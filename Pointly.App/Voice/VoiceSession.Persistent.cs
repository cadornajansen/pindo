using System.Diagnostics;
using System.Threading.Channels;

namespace Pointly.App.Voice;

public sealed partial class VoiceSession
{
    // One owner serializes device/socket disposal before a replacement may acquire the microphone.
    private readonly object _persistentGate = new();
    private readonly SemaphoreSlim _persistentOwner = new(1, 1);
    private CancellationTokenSource? _persistentCancellation;
    private Channel<Narration>? _narrations;
    private TaskCompletionSource _activityPulse = NewSignal();
    private long _persistentIdentity;
    private volatile bool _muted;
    private VoiceActivity _activity = new(false, false, false, false, false);
    public bool IsActive => Activity.Active;
    public long SessionIdentity => Interlocked.Read(ref _persistentIdentity);
    public VoiceActivity Activity => Volatile.Read(ref _activity);
    public event Action<VoiceActivity>? ActivityChanged;
    internal Task PersistentCompletion { get; private set; } = Task.CompletedTask;

    private sealed record Narration(string Text, CancellationToken Token, TaskCompletionSource Done);
    private sealed record AcceptedTurn(string Text, long ReceivedAt);
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Summon(bool muted = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel("SessionReplaced");
        _muted = muted;
        long identity;
        var cancellation = new CancellationTokenSource();
        var queue = Channel.CreateBounded<Narration>(8);
        lock (_persistentGate)
        {
            identity = Interlocked.Increment(ref _persistentIdentity);
            _persistentCancellation = cancellation;
            _narrations = queue;
            PublishActivity(new(true, false, false, false, muted));
        }
        _telemetry("VoiceSessionStarted Mode=Persistent");
        PersistentCompletion = Task.Run(() => RunPersistentAsync(identity, queue, cancellation));
    }

    public void Interrupt()
    {
        if (!IsActive) return;
        var timer = Stopwatch.StartNew();
        Summon(_muted);
        _telemetry($"InterruptToPlaybackStopRequestedMs={timer.ElapsedMilliseconds}");
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        if (muted) _capture?.Stop();
        PublishActivity(Activity with { Muted = muted, MicrophoneOn = muted ? false : Activity.MicrophoneOn });
        Interlocked.Exchange(ref _activityPulse, NewSignal()).TrySetResult();
    }

    private void StopPersistent(string reason)
    {
        lock (_persistentGate)
        {
            if (_persistentCancellation is null) return;
            Interlocked.Increment(ref _persistentIdentity);
            CancelSafely(_persistentCancellation);
            _persistentCancellation = null;
            _capture?.Stop();
            CancelSafely(_speechCancellation);
            _playback.Stop();
            _narrations?.Writer.TryComplete();
            _narrations = null;
            PublishActivity(new(false, false, false, false, _muted));
            _telemetry($"PersistentSessionCancelled Reason={reason}");
        }
    }

    private void PublishPersistent(long identity, VoiceActivity activity)
    {
        lock (_persistentGate)
            if (CurrentPersistent(identity)) PublishActivity(activity);
    }
    private void SetPersistentState(long identity, VoiceSessionState state, string? detail = null)
    {
        lock (_persistentGate)
            if (CurrentPersistent(identity)) SetState(state, detail);
    }

    private bool CurrentPersistent(long id) => !_disposed && id == Interlocked.Read(ref _persistentIdentity);
    private void PublishActivity(VoiceActivity activity)
    {
        Volatile.Write(ref _activity, activity);
        ActivityChanged?.Invoke(activity);
    }

    private async Task QueueNarrationAsync(string instruction, CancellationToken token)
    {
        Channel<Narration>? queue = _narrations;
        if (queue is null) return;
        var narration = new Narration(instruction, token, NewSignal());
        await queue.Writer.WriteAsync(narration, token);
        await narration.Done.Task.WaitAsync(token);
    }

    private async Task RunPersistentAsync(long identity, Channel<Narration> queue, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
        bool owned = false;
        try
        {
            await _persistentOwner.WaitAsync(token);
            owned = true;
            while (CurrentPersistent(identity))
            {
                token.ThrowIfCancellationRequested();
                Task activityPulse = _activityPulse.Task;
                if (queue.Reader.TryRead(out Narration? narration))
                {
                    if (narration.Token.IsCancellationRequested) { narration.Done.TrySetCanceled(); continue; }
                    using var speech = CancellationTokenSource.CreateLinkedTokenSource(token, narration.Token);
                    try
                    {
                        PublishPersistent(identity, new(true, false, false, true, _muted));
                        await PlaySpeechAsync(narration.Text, speech.Token);
                        narration.Done.TrySetResult();
                    }
                    catch (OperationCanceledException) when (speech.IsCancellationRequested) { narration.Done.TrySetCanceled(); }
                    catch (Exception ex) { narration.Done.TrySetException(ex); throw; }
                    continue;
                }
                if (_muted)
                {
                    PublishPersistent(identity, new(true, false, false, false, true));
                    SetPersistentState(identity, VoiceSessionState.Idle, "MicrophoneMuted");
                    using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
                    await Task.WhenAny(activityPulse, queue.Reader.WaitToReadAsync(waiting.Token).AsTask()).WaitAsync(token);
                    waiting.Cancel();
                    continue;
                }

                AcceptedTurn? transcript = await ListenForTurnAsync(identity, queue, token);
                if (!CurrentPersistent(identity)) return;
                if (transcript is null) continue;
                FinalTranscript?.Invoke(transcript.Text);
                PublishPersistent(identity, new(true, false, true, false, _muted));
                SetPersistentState(identity, VoiceSessionState.Processing);
                _telemetry($"FinalTranscriptToProcessingStartMs={Stopwatch.GetElapsedTime(transcript.ReceivedAt).TotalMilliseconds:F0}");
                var timer = Stopwatch.StartNew();
                using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
                request.CancelAfter(TimeSpan.FromSeconds(60));
                try
                {
                    string? instruction = await _processTranscript(transcript.Text, request.Token).WaitAsync(request.Token);
                    if (!CurrentPersistent(identity)) return;
                    _telemetry($"VoiceProcessingCompleted DurationMs={timer.ElapsedMilliseconds}");
                    if (!string.IsNullOrWhiteSpace(instruction))
                    {
                        PublishPersistent(identity, new(true, false, false, true, _muted));
                        await PlaySpeechAsync(instruction, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _telemetry($"VoiceTurnFailed Reason={(ex is VoiceException known ? known.Reason : ex.GetType().Name)}");
                    SetPersistentState(identity, VoiceSessionState.Failed, "RequestFailed");
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (CurrentPersistent(identity))
            {
                string reason = ex is VoiceException known ? known.Reason : ex.GetType().Name;
                _telemetry($"VoiceSessionFailed Reason={reason}");
                SetPersistentState(identity, VoiceSessionState.Failed, reason);
            }
        }
        finally
        {
            while (queue.Reader.TryRead(out Narration? pending)) pending.Done.TrySetCanceled();
            queue.Writer.TryComplete();
            if (owned) _persistentOwner.Release();
            lock (_persistentGate)
            {
                if (CurrentPersistent(identity))
                {
                    _persistentCancellation = null;
                    _narrations = null;
                    PublishPersistent(identity, new(false, false, false, false, _muted));
                    SetPersistentState(identity, VoiceSessionState.Idle);
                }
                cancellation.Dispose();
            }
        }
    }

    private async Task<AcceptedTurn?> ListenForTurnAsync(long identity, Channel<Narration> queue, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_muted) return null;
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(token);
        using IAudioCapture capture = _captureFactory();
        var final = new TaskCompletionSource<AcceptedTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool hasPartial = false;
        long lastSegment = 0;
        int accepted = 0;
        _capture = capture;
        var timer = Stopwatch.StartNew();
        capture.Start();
        token.ThrowIfCancellationRequested();
        if (_muted) return null;
        _telemetry($"MicrophoneStartMs={timer.ElapsedMilliseconds}");
        PublishPersistent(identity, new(true, true, false, false, _muted));
        SetPersistentState(identity, VoiceSessionState.Listening);
        Task receive = _speechToText.ListenAsync(capture.CaptureAsync(listening.Token), partial =>
        {
            if (!CurrentPersistent(identity) || listening.IsCancellationRequested || Volatile.Read(ref accepted) != 0) return;
            Volatile.Write(ref hasPartial, !string.IsNullOrWhiteSpace(partial));
            PartialTranscript?.Invoke(partial);
        }, segment =>
        {
            if (!CurrentPersistent(identity) || listening.IsCancellationRequested || segment.Sequence <= lastSegment) return;
            lastSegment = segment.Sequence;
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                Volatile.Write(ref hasPartial, false);
                if (queue.Reader.TryPeek(out _)) Interlocked.Exchange(ref _activityPulse, NewSignal()).TrySetResult();
                return;
            }
            if (Interlocked.Exchange(ref accepted, 1) != 0) return;
            _telemetry($"SpeechFinalReceived CharacterCount={segment.Text.Length} SpeechEndToFinalMs=Unavailable");
            final.TrySetResult(new(segment.Text.Trim(), Stopwatch.GetTimestamp()));
        }, ms => _telemetry($"SpeechToTextConnected SttConnectMs={ms}"), listening.Token);
        try
        {
            while (true)
            {
                Task pulse = _activityPulse.Task;
                if (_muted) return null;
                Task narration = queue.Reader.WaitToReadAsync(listening.Token).AsTask();
                await Task.WhenAny(receive, final.Task, pulse, narration).WaitAsync(token);
                if (final.Task.IsCompleted) return await final.Task;
                if (_muted) return null;
                if (receive.IsCompleted) { await receive; throw new VoiceException("STT", "StreamEndedUnexpectedly"); }
                // An unmute pulse alone must not discard the newly opened speech connection.
                if (queue.Reader.TryPeek(out _))
                {
                    if (!Volatile.Read(ref hasPartial)) return null;
                    // A step may resolve while the user is talking. Defer narration until the utterance commits.
                    await Task.WhenAny(receive, final.Task, pulse).WaitAsync(token);
                }
            }
        }
        finally
        {
            // Close this audio epoch before processing/TTS. Speaker audio cannot contaminate a later segment.
            Interlocked.Exchange(ref accepted, 1);
            listening.Cancel();
            capture.Stop();
            PublishPersistent(identity, new(true, false, false, false, _muted));
            if (final.Task.IsCompletedSuccessfully) SetPersistentState(identity, VoiceSessionState.Finalizing);
            // A Stop() can end the capture iterator just before cancellation reaches the socket.
            // The primary receive failure has already been observed in the try body if it caused this exit.
            try { await receive; } catch (Exception) when (listening.IsCancellationRequested) { }
            if (ReferenceEquals(_capture, capture)) _capture = null;
        }
    }
}
