using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Pointly.App.Voice;
using Pointly.App.Walkthrough;

namespace Pointly.Tests;

public sealed class PersistentVoiceTests
{
    [Fact]
    public async Task FiftyCyclesReleaseResourcesAndRejectEchoDuplicatesAndLateCallbacks()
    {
        var hardware = new Hardware();
        var stt = new Speech();
        var playback = new Playback(hardware);
        int turns = 0;
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), playback,
            (_, _) => { Interlocked.Increment(ref turns); return Task.FromResult<string?>("Instruction"); }, _ => { });
        for (int i = 0; i < 50; i++)
        {
            voice.Summon();
            Speech.Connection first = await stt.Next();
            Assert.True(voice.Activity.MicrophoneOn);
            first.Final(new(1, ""));
            Assert.Equal(i * 2, turns);
            first.Final(new(2, "same intentional question"));
            first.Final(new(2, "same intentional question"));
            await Until(() => playback.Calls == i * 2 + 1);
            Assert.Equal(0, hardware.Active);
            first.Final(new(3, "simulated speaker echo"));
            playback.Finish();
            Speech.Connection second = await stt.Next();
            second.Final(new(1, "same intentional question"));
            await Until(() => playback.Calls == i * 2 + 2);
            playback.Finish();
            Speech.Connection third = await stt.Next();
            voice.Cancel("TestDismissed");
            third.Final(new(1, "late"));
            await voice.PersistentCompletion.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(voice.IsActive);
            Assert.Equal(0, hardware.Active);
            Assert.Equal((i + 1) * 2, turns);
        }
        Assert.Equal(1, hardware.Maximum);
        Assert.Equal(150, hardware.Disposed);
    }

    [Theory]
    [InlineData("Listening")]
    [InlineData("Processing")]
    [InlineData("Speaking")]
    public async Task DismissAndReplacementInvalidateEveryActiveStage(string stage)
    {
        var hardware = new Hardware(); var stt = new Speech(); var playback = new Playback(hardware);
        var processing = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), playback,
            (_, token) => stage == "Processing" ? processing.Task.WaitAsync(token) : Task.FromResult<string?>("Say"), _ => { });
        voice.Summon();
        var first = await stt.Next();
        if (stage != "Listening")
        {
            first.Final(new(1, "question"));
            await Until(() => stage == "Processing" ? voice.Activity.Processing : playback.Calls == 1);
        }
        voice.Interrupt();
        var fresh = await stt.Next();
        Assert.True(voice.IsActive);
        first.Final(new(2, "stale"));
        Assert.True(voice.Activity.MicrophoneOn);
        voice.Cancel("Dismiss");
        fresh.Final(new(1, "stale"));
        await voice.PersistentCompletion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, hardware.Active);
        Assert.False(voice.IsActive);
        Assert.Equal(1, hardware.Maximum);
    }

    [Fact]
    public async Task NarrationDefersDuringPartialAndMutedSessionDoesNotCapture()
    {
        var hardware = new Hardware(); var stt = new Speech(); var playback = new Playback(hardware);
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), playback,
            (_, _) => Task.FromResult<string?>(null), _ => { });
        voice.Summon(true);
        Assert.True(voice.IsActive);
        Assert.Equal(0, hardware.Active);
        voice.SetMuted(false);
        var listen = await stt.Next();
        listen.Partial("user is speaking");
        using var obsolete = new CancellationTokenSource();
        Task narration = voice.SpeakAsync("Old step", obsolete.Token);
        await Task.Delay(30);
        Assert.Equal(0, playback.Calls);
        obsolete.Cancel();
        listen.Final(new(1, "new question"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => narration);
        await stt.Next();
        Assert.Equal(0, playback.Calls);
        voice.Cancel("Done"); await voice.PersistentCompletion;
    }

    [Fact]
    public async Task NarrationDrainsThenResumesMicWithoutAdvancingWalkthrough()
    {
        var hardware = new Hardware(); var stt = new Speech(); var playback = new Playback(hardware);
        var walkthrough = new WalkthroughService();
        var step = walkthrough.Start(DemoWalkthroughs.ExcelPivotTable);
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), playback,
            (_, _) => Task.FromResult<string?>(null), _ => { });
        voice.Summon(); var listening = await stt.Next();
        Task speech = voice.SpeakAsync(step.CurrentStep!.Instruction, CancellationToken.None);
        await Until(() => playback.Calls == 1);
        Assert.Equal(0, hardware.Active);
        listening.Final(new(1, "speaker echo"));
        Assert.Equal(0, walkthrough.Current!.StepIndex);
        playback.Finish(); await speech; await stt.Next();
        Assert.True(voice.Activity.MicrophoneOn);
        Assert.Equal(0, walkthrough.Current!.StepIndex);
        Assert.Equal(1, walkthrough.Hit(step.SessionId, 0)!.StepIndex);
        voice.Cancel("Done"); await voice.PersistentCompletion;
    }

    [Fact]
    public async Task MuteStopsCaptureAndUnmuteReopensWithinSameSession()
    {
        var hardware = new Hardware(); var stt = new Speech();
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), new Playback(hardware),
            (_, _) => Task.FromResult<string?>(null), _ => { });
        voice.Summon(); await stt.Next();
        long identity = voice.SessionIdentity;
        voice.SetMuted(true);
        await Until(() => hardware.Active == 0);
        Assert.True(voice.IsActive);
        Assert.False(voice.Activity.MicrophoneOn);
        voice.SetMuted(false); await stt.Next();
        Assert.Equal(identity, voice.SessionIdentity);
        Assert.True(voice.Activity.MicrophoneOn);
        voice.Cancel("Done"); await voice.PersistentCompletion;
    }

    [Fact]
    public async Task FailureStopsCaptureWithoutReconnectLoopAndCanBeSummonedAgain()
    {
        var hardware = new Hardware(); var stt = new Speech { Fail = true };
        using var voice = new VoiceSession(() => new Capture(hardware), stt, new Tts(), new Playback(hardware),
            (_, _) => Task.FromResult<string?>(null), _ => { });
        voice.Summon(); await voice.PersistentCompletion;
        Assert.False(voice.IsActive); Assert.Equal(0, hardware.Active);
        stt.Fail = false;
        voice.Summon(); await stt.Next();
        voice.Cancel("Done"); await voice.PersistentCompletion;
        Assert.Equal(0, hardware.Active);
    }

    private static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(3000);
        while (!predicate()) await Task.Delay(5, timeout.Token);
    }
    private sealed class Hardware { public int Active; public int Maximum; public int Disposed; }
    private sealed class Capture(Hardware hardware) : IAudioCapture
    {
        private bool disposed;
        public MicrophoneAudioSummary AudioSummary => new(0, 0, 0, 0, 0);
        public void Start() { hardware.Active++; hardware.Maximum = Math.Max(hardware.Maximum, hardware.Active); }
        public void Stop() { }
        public async IAsyncEnumerable<byte[]> CaptureAsync([EnumeratorCancellation] CancellationToken token)
        { await Task.Delay(Timeout.Infinite, token); yield break; }
        public void Dispose() { if (disposed) return; disposed = true; hardware.Active--; hardware.Disposed++; }
    }
    private sealed class Speech : ISpeechToText
    {
        public sealed record Connection(Action<string> Partial, Action<SpeechSegment> Final);
        private readonly ConcurrentQueue<Connection> pending = new();
        public bool Fail;
        public async Task<Connection> Next()
        { await Until(() => !pending.IsEmpty); Assert.True(pending.TryDequeue(out var c)); return c!; }
        public async Task ListenAsync(IAsyncEnumerable<byte[]> audio, Action<string> partial,
            Action<SpeechSegment> final, Action<long> connected, CancellationToken token)
        {
            if (Fail) throw new VoiceException("STT", "MissingElevenLabsApiKey");
            pending.Enqueue(new(partial, final)); connected(1);
            await Task.Delay(Timeout.Infinite, token);
        }
        public Task<string?> TranscribeAsync(IAsyncEnumerable<byte[]> audio, Action<string> partial,
            Action<long> connected, CancellationToken token, Action<SttAudioSummary>? summary = null) => throw new NotSupportedException();
        public void Dispose() { }
    }
    private sealed class Tts : ITextToSpeech
    {
        public string ModelId => "mock";
        public async IAsyncEnumerable<byte[]> StreamAsync(string text, [EnumeratorCancellation] CancellationToken token)
        { await Task.Yield(); token.ThrowIfCancellationRequested(); yield return [0, 0]; }
        public void Dispose() { }
    }
    private sealed class Playback(Hardware hardware) : IAudioPlayback
    {
        private TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public async Task PlayAsync(IAsyncEnumerable<byte[]> audio, Action<long> first, CancellationToken token)
        {
            Assert.Equal(0, hardware.Active);
            drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Increment(ref Calls);
            await foreach (byte[] chunk in audio.WithCancellation(token)) Array.Clear(chunk);
            first(1); await drained.Task.WaitAsync(token);
        }
        public void Finish() => drained.TrySetResult();
        public void Stop() => drained.TrySetCanceled();
        public void Dispose() { }
    }
}
