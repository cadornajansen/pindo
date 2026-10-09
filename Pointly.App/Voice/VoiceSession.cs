using System.Diagnostics;

namespace Pointly.App.Voice;

public sealed partial class VoiceSession : IVoiceSession
{
    private readonly Func<IAudioCapture> _captureFactory;
    private readonly ISpeechToText _speechToText;
    private readonly ITextToSpeech _textToSpeech;
    private readonly IAudioPlayback _playback;
    private readonly Func<string, CancellationToken, Task<string?>> _processTranscript;
    private readonly Action<string> _telemetry;
    private CancellationTokenSource? _sessionCancellation;
    private CancellationTokenSource? _speechCancellation;
    private IAudioCapture? _capture;
    private long _generation;
    private bool _disposed;
    private long _speechEndTimestamp;
    private readonly SemaphoreSlim _speechOwner = new(1, 1);

    public VoiceSessionState State { get; private set; } = VoiceSessionState.Idle;
    public event Action<VoiceSessionEvent>? StateChanged;
    public event Action<string>? PartialTranscript;
    public event Action<string>? FinalTranscript;

    public VoiceSession(Func<IAudioCapture> captureFactory, ISpeechToText speechToText,
        ITextToSpeech textToSpeech, IAudioPlayback playback,
        Func<string, CancellationToken, Task<string?>> processTranscript,
        Action<string> telemetry)
    {
        _captureFactory = captureFactory;
        _speechToText = speechToText;
        _textToSpeech = textToSpeech;
        _playback = playback;
        _processTranscript = processTranscript;
        _telemetry = telemetry;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel("ReplacedByNewSession");
        long generation = ++_generation;
        _speechEndTimestamp = 0;
        var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _sessionCancellation = cancellation;
        SetState(VoiceSessionState.Listening);
        _telemetry("VoiceSessionStarted");
        _ = RunAsync(generation, cancellation);
    }

    public void StopListening()
    {
        if (State != VoiceSessionState.Listening) return;
        SetState(VoiceSessionState.Finalizing);
        _speechEndTimestamp = Stopwatch.GetTimestamp();
        _capture?.Stop();
    }

    public void Cancel(string reason)
    {
        StopPersistent(reason);
        if (State == VoiceSessionState.Idle) return;
        ++_generation;
        CancelSafely(_sessionCancellation);
        _capture?.Stop();
        CancelSafely(_speechCancellation);
        _playback.Stop();
        SetState(VoiceSessionState.Cancelled, reason);
        _telemetry($"VoiceSessionCancelled Reason={reason}");
        SetState(VoiceSessionState.Idle);
    }

    private async Task RunAsync(long generation, CancellationTokenSource cancellation)
    {
        var sttTimer = Stopwatch.StartNew();
        IAudioCapture? capture = null;
        try
        {
            capture = _captureFactory();
            _capture = capture;
            var microphone = Stopwatch.StartNew();
            capture.Start();
            _telemetry($"MicrophoneStartMs={microphone.ElapsedMilliseconds}");
            SttAudioSummary sentAudio = SttAudioSummary.Empty;
            string? transcript = await _speechToText.TranscribeAsync(
                capture.CaptureAsync(cancellation.Token),
                partial =>
                {
                    if (IsCurrent(generation))
                    {
                        PartialTranscript?.Invoke(partial);
                        _telemetry($"SpeechPartialReceived CharacterCount={partial.Length}");
                    }
                },
                connectMs => { if (IsCurrent(generation)) _telemetry($"SpeechToTextConnected SttConnectMs={connectMs}"); },
                cancellation.Token, summary => sentAudio = summary);
            // A provider can finish early; never leave the microphone open while processing or speaking.
            capture.Stop();
            capture.Dispose();
            if (!IsCurrent(generation)) return;
            _capture = null;
            transcript = transcript?.Trim();
            _telemetry($"SpeechFinalReceived CharacterCount={transcript?.Length ?? 0} SttDurationMs={sttTimer.ElapsedMilliseconds}");
            if (_speechEndTimestamp != 0)
                _telemetry($"SpeechEndToFinalMs={Stopwatch.GetElapsedTime(_speechEndTimestamp).TotalMilliseconds:F0}");
            if (string.IsNullOrEmpty(transcript))
            {
                MicrophoneAudioSummary captured = capture.AudioSummary;
                _telemetry(FormattableString.Invariant($"SpeechFinalEmpty CapturedChunks={captured.Chunks} CapturedBytes={captured.Bytes} SentChunks={sentAudio.ChunksSent} SentBytes={sentAudio.BytesSent} Peak={captured.PeakMax:F5} Rms={captured.RmsAverage:F5} DroppedChunks={captured.DroppedChunks}"));
                SetState(VoiceSessionState.Idle, "EmptyTranscript");
                return;
            }
            FinalTranscript?.Invoke(transcript);
            SetState(VoiceSessionState.Processing);
            _telemetry("VoiceProcessingStarted");
            var processing = Stopwatch.StartNew();
            string? instruction = await _processTranscript(transcript, cancellation.Token);
            if (!IsCurrent(generation)) return;
            _telemetry($"VoiceProcessingCompleted DurationMs={processing.ElapsedMilliseconds}");
            if (!string.IsNullOrWhiteSpace(instruction))
                await SpeakAsync(instruction, cancellation.Token);
            if (IsCurrent(generation)) SetState(VoiceSessionState.Idle);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (IsCurrent(generation))
            {
                string stage = ex is VoiceException voice ? voice.Stage : State.ToString();
                string reason = ex is VoiceException known ? known.Reason : ex.GetType().Name;
                SetState(VoiceSessionState.Failed, reason);
                _telemetry($"VoiceSessionFailed Stage={stage} Reason={reason}");
                SetState(VoiceSessionState.Idle);
            }
        }
        finally
        {
            capture?.Dispose();
            if (IsCurrent(generation))
            {
                _capture = null;
                _sessionCancellation = null;
                if (State != VoiceSessionState.Idle) SetState(VoiceSessionState.Idle);
            }
            cancellation.Dispose();
        }
    }

    public async Task SpeakAsync(string instruction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return;
        if (IsActive)
        {
            await QueueNarrationAsync(instruction, cancellationToken);
            return;
        }
        await PlaySpeechAsync(instruction, cancellationToken);
    }

    private async Task PlaySpeechAsync(string instruction, CancellationToken cancellationToken)
    {
        CancelSafely(_speechCancellation);
        cancellationToken.ThrowIfCancellationRequested();
        var speech = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        speech.CancelAfter(TimeSpan.FromSeconds(45));
        _speechCancellation = speech;
        var timer = Stopwatch.StartNew();
        bool owned = false;
        try
        {
            await _speechOwner.WaitAsync(speech.Token);
            owned = true;
            SetState(VoiceSessionState.Speaking);
            _telemetry($"TtsStarted Model={_textToSpeech.ModelId}");
            await _playback.PlayAsync(_textToSpeech.StreamAsync(instruction, speech.Token),
                firstAudioMs => _telemetry($"TtsTimeToFirstAudioMs={firstAudioMs}"), speech.Token);
            _telemetry($"TtsCompleted DurationMs={timer.ElapsedMilliseconds}");
        }
        finally
        {
            if (owned) _speechOwner.Release();
            if (ReferenceEquals(_speechCancellation, speech))
            {
                _speechCancellation = null;
                if (State == VoiceSessionState.Speaking) SetState(VoiceSessionState.Idle);
            }
            speech.Dispose();
        }
    }

    private static void CancelSafely(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { } // Completion can win the race with an explicit interruption.
    }

    private bool IsCurrent(long generation) => !_disposed && generation == _generation;
    private void SetState(VoiceSessionState state, string? detail = null)
    {
        State = state;
        StateChanged?.Invoke(new VoiceSessionEvent(state, detail));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel("ApplicationClosing");
        _disposed = true;
        _speechToText.Dispose();
        _textToSpeech.Dispose();
        _playback.Dispose();
    }
}
