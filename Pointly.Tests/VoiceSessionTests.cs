using System.Runtime.CompilerServices;
using Pointly.App.Voice;

namespace Pointly.Tests;

public sealed class VoiceSessionTests
{
    [Fact]
    public async Task FinalTranscriptRunsProcessingAndSpeakingOnce()
    {
        var capture = new FakeCapture();
        var speech = new FakeSpeechToText();
        var playback = new FakePlayback();
        int processingCount = 0;
        using var session = Create(capture, speech, playback, (text, _) =>
        {
            Assert.True(capture.IsDisposed);
            Assert.Equal("PivotTable", text);
            processingCount++;
            return Task.FromResult<string?>("Open Insert.");
        });
        var states = new List<VoiceSessionState>();
        session.StateChanged += change => states.Add(change.State);

        session.Start();
        Assert.Equal(VoiceSessionState.Listening, session.State);
        session.StopListening();
        Assert.Equal(VoiceSessionState.Finalizing, session.State);
        speech.Finish(" PivotTable ");
        await WaitUntilAsync(() => session.State == VoiceSessionState.Idle && playback.PlayCount == 1);

        Assert.Equal(1, processingCount);
        Assert.Contains(VoiceSessionState.Processing, states);
        Assert.Contains(VoiceSessionState.Speaking, states);
        Assert.Equal(1, playback.PlayCount);
    }

    [Fact]
    public async Task EmptyFinalDoesNotRunTutor()
    {
        var speech = new FakeSpeechToText();
        var capture = new FakeCapture();
        var playback = new FakePlayback();
        var telemetry = new List<string>();
        var states = new List<VoiceSessionEvent>();
        int processingCount = 0;
        using var session = new VoiceSession(() => capture, speech, new FakeTts(), playback, (_, _) =>
        {
            processingCount++;
            return Task.FromResult<string?>("Instruction");
        }, telemetry.Add);
        session.StateChanged += states.Add;
        session.Start();
        session.StopListening();
        speech.Finish("   ");
        await WaitUntilAsync(() => session.State == VoiceSessionState.Idle);
        Assert.Equal(0, processingCount);
        Assert.Equal(0, playback.PlayCount);
        Assert.True(capture.IsDisposed);
        Assert.Contains(states, value => value.State == VoiceSessionState.Idle && value.Detail == "EmptyTranscript");
        Assert.Contains(telemetry, value => value == "SpeechFinalEmpty CapturedChunks=2 CapturedBytes=6400 SentChunks=2 SentBytes=6400 Peak=0.50000 Rms=0.25000 DroppedChunks=0");
    }

    [Fact]
    public async Task CancelledAndReplacedSessionsIgnoreOldFinal()
    {
        var speech = new FakeSpeechToText();
        int processingCount = 0;
        using var session = Create(new FakeCapture(), speech, new FakePlayback(), (_, _) =>
        {
            processingCount++;
            return Task.FromResult<string?>(null);
        });
        session.Start();
        session.Cancel("Test");
        Assert.Equal(VoiceSessionState.Idle, session.State);
        speech.Finish("stale");
        await Task.Delay(50);
        Assert.Equal(0, processingCount);

        session.Start();
        session.Start();
        Assert.Equal(VoiceSessionState.Listening, session.State);
        session.Cancel("Test");
        Assert.Equal(0, processingCount);
    }

    [Fact]
    public async Task CancellationDuringProcessingPreventsSpeech()
    {
        var speech = new FakeSpeechToText();
        var playback = new FakePlayback();
        var processing = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = Create(new FakeCapture(), speech, playback, (_, _) => processing.Task);
        session.Start();
        session.StopListening();
        speech.Finish("PivotTable");
        await WaitUntilAsync(() => session.State == VoiceSessionState.Processing);
        session.Cancel("Test");
        processing.SetResult("Open Insert.");
        await Task.Delay(50);
        Assert.Equal(VoiceSessionState.Idle, session.State);
        Assert.Equal(0, playback.PlayCount);
    }

    [Fact]
    public async Task CancellationDuringSpeakingStopsPlayback()
    {
        var speech = new FakeSpeechToText();
        var playback = new FakePlayback { BlockAfterAudio = true };
        using var session = Create(new FakeCapture(), speech, playback,
            (_, _) => Task.FromResult<string?>("Open Insert."));
        session.Start();
        session.StopListening();
        speech.Finish("PivotTable");
        await WaitUntilAsync(() => session.State == VoiceSessionState.Speaking && playback.PlayCount > 0);
        session.Cancel("Test");
        Assert.Equal(VoiceSessionState.Idle, session.State);
        Assert.True(playback.StopCount > 0);
    }

    private static VoiceSession Create(FakeCapture capture, FakeSpeechToText speech,
        FakePlayback playback, Func<string, CancellationToken, Task<string?>> process) =>
        new(() => capture, speech, new FakeTts(), playback, process, _ => { });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class FakeCapture : IAudioCapture
    {
        public MicrophoneAudioSummary AudioSummary { get; } = new(2, 6400, 0.5, 0.25, 0);
        public bool IsDisposed { get; private set; }
        public void Start() { }
        public async IAsyncEnumerable<byte[]> CaptureAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return [0, 0];
            await Task.CompletedTask;
        }
        public void Stop() { }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeSpeechToText : ISpeechToText
    {
        private readonly Queue<TaskCompletionSource<string?>> _pending = new();
        public async Task<string?> TranscribeAsync(IAsyncEnumerable<byte[]> audio,
            Action<string> onPartial, Action<long> onConnected, CancellationToken cancellationToken,
            Action<SttAudioSummary>? onAudioSummary = null)
        {
            onConnected(1);
            var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Enqueue(completion);
            string? result = await completion.Task;
            onAudioSummary?.Invoke(new SttAudioSummary(2, 6400));
            return result;
        }
        public void Finish(string text) => _pending.Dequeue().TrySetResult(text);
        public void Dispose() { }
    }

    private sealed class FakeTts : ITextToSpeech
    {
        public string ModelId => "fake";
        public async IAsyncEnumerable<byte[]> StreamAsync(string text,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return [0, 0];
            await Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private sealed class FakePlayback : IAudioPlayback
    {
        public int PlayCount { get; private set; }
        public int StopCount { get; private set; }
        public bool BlockAfterAudio { get; init; }
        public async Task PlayAsync(IAsyncEnumerable<byte[]> audio, Action<long> onFirstAudio,
            CancellationToken cancellationToken)
        {
            await foreach (byte[] _ in audio.WithCancellation(cancellationToken))
            {
                PlayCount++;
                onFirstAudio(1);
            }
            if (BlockAfterAudio) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public void Stop() => StopCount++;
        public void Dispose() { }
    }
}
