using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Pointly.App.Voice;

namespace Pointly.Tests;

public sealed class ElevenLabsSpeechToTextTests
{
    [Fact]
    public async Task SendsPcmImmediatelyWithoutWaitingForRecordingToFinish()
    {
        using var socket = ReadySocket();
        var audio = Channel.CreateUnbounded<byte[]>();
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            _ => { }, CancellationToken.None);
        byte[] chunk = PcmChunk(19);

        await audio.Writer.WriteAsync(chunk);
        SentAudio sent = await socket.NextSentAsync();

        Assert.Equal("input_audio_chunk", sent.MessageType);
        Assert.Equal(16_000, sent.SampleRate);
        Assert.False(sent.Commit);
        Assert.Equal(3_200, sent.Audio.Length);
        Assert.All(sent.Audio, sampleByte => Assert.Equal((byte)19, sampleByte));
        Assert.False(result.IsCompleted);

        audio.Writer.Complete();
        Assert.True((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"Create a PivotTable"}""");
        Assert.Equal("Create a PivotTable", await FinishAsync(result));
        Assert.All(chunk, sampleByte => Assert.Equal((byte)0, sampleByte));
    }

    [Fact]
    public async Task DrainsAllQueuedChunksInOrderBeforeOneSeparateCommit()
    {
        using var socket = ReadySocket();
        var audio = Channel.CreateUnbounded<byte[]>();
        var chunks = new[] { PcmChunk(1), PcmChunk(2), PcmChunk(3) };
        foreach (byte[] chunk in chunks) await audio.Writer.WriteAsync(chunk);
        audio.Writer.Complete();
        SttAudioSummary? summary = null;
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            _ => { }, CancellationToken.None, value => summary = value);

        for (byte expected = 1; expected <= 3; expected++)
        {
            SentAudio sent = await socket.NextSentAsync();
            Assert.False(sent.Commit);
            Assert.All(sent.Audio, value => Assert.Equal(expected, value));
        }
        SentAudio commit = await socket.NextSentAsync();
        Assert.True(commit.Commit);
        Assert.Empty(commit.Audio);
        Assert.Equal(16_000, commit.SampleRate);
        Assert.False(result.IsCompleted);

        socket.Deliver("""{"message_type":"committed_transcript","text":"done"}""");
        Assert.Equal("done", await FinishAsync(result));
        Assert.Equal(4, socket.SentCount);
        Assert.NotNull(summary);
        Assert.Equal(3, summary.ChunksSent);
        Assert.Equal(9_600, summary.BytesSent);
        foreach (byte[] chunk in chunks) Assert.All(chunk, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public async Task CommitKeepsSocketOpenAndWaitsForCommittedTranscript()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(PcmChunk(4)),
            _ => { }, CancellationToken.None);
        Assert.False((await socket.NextSentAsync()).Commit);
        Assert.True((await socket.NextSentAsync()).Commit);

        Assert.False(result.IsCompleted);
        Assert.Equal(0, socket.CloseOutputCount);
        socket.Deliver("""{"message_type":"partial_transcript","text":"part"}""");
        Assert.False(result.IsCompleted);
        socket.Deliver("""{"message_type":"committed_transcript","text":"complete"}""");

        Assert.Equal("complete", await FinishAsync(result));
    }

    [Fact]
    public async Task PartialTranscriptIsEmittedWithoutCompletingRequest()
    {
        using var socket = ReadySocket();
        var audio = Channel.CreateUnbounded<byte[]>();
        var partial = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            value => partial.TrySetResult(value), CancellationToken.None);
        socket.Deliver("""{"message_type":"partial_transcript","text":"How do I"}""");

        Assert.Equal("How do I", await partial.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(result.IsCompleted);
        await audio.Writer.WriteAsync(PcmChunk(5));
        audio.Writer.Complete();
        Assert.False((await socket.NextSentAsync()).Commit);
        Assert.True((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"How do I create a PivotTable?"}""");
        Assert.Equal("How do I create a PivotTable?", await FinishAsync(result));
    }

    [Fact]
    public async Task EmptyCommittedTranscriptReturnsEmptyWithoutFabricatingText()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(PcmChunk(0)),
            _ => { }, CancellationToken.None);
        await socket.NextSentAsync();
        Assert.True((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":""}""");

        Assert.Equal(string.Empty, await FinishAsync(result));
    }

    [Fact]
    public async Task ServerEventDiagnosticsExcludeTranscriptAndProviderMessages()
    {
        const string sensitive = "PRIVATE_PROVIDER_CONTENT_SENTINEL";
        using var socket = ReadySocket();
        var diagnostics = new ConcurrentQueue<string>();
        using var provider = new ElevenLabsSpeechToText(diagnostics.Enqueue);
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(PcmChunk(6)),
            _ => { }, CancellationToken.None);
        await socket.NextSentAsync();
        await socket.NextSentAsync();
        socket.Deliver($$"""{"message_type":"warning","message":"{{sensitive}}"}""");
        socket.Deliver($$"""{"message_type":"partial_transcript","text":"{{sensitive}}"}""");
        socket.Deliver($$"""{"message_type":"committed_transcript","text":"{{sensitive}}"}""");
        Assert.Equal(sensitive, await FinishAsync(result));

        Assert.Contains(diagnostics, value => value.Contains("session_started", StringComparison.Ordinal));
        Assert.Contains(diagnostics, value => value.Contains("warning", StringComparison.Ordinal));
        Assert.Contains(diagnostics, value => value.Contains("partial_transcript", StringComparison.Ordinal));
        Assert.Contains(diagnostics, value => value.Contains("committed_transcript", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, value => value.Contains(sensitive, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("auth_error")]
    [InlineData("input_error")]
    [InlineData("invalid_request")]
    public async Task ServerErrorsFailSafelyAndAreObservable(string eventType)
    {
        const string sensitive = "PRIVATE_ERROR_PAYLOAD_SENTINEL";
        using var socket = ReadySocket();
        var diagnostics = new ConcurrentQueue<string>();
        using var provider = new ElevenLabsSpeechToText(diagnostics.Enqueue);
        var audio = Channel.CreateUnbounded<byte[]>();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            _ => { }, CancellationToken.None);
        socket.Deliver($$"""{"message_type":"{{eventType}}","message":"{{sensitive}}"}""");

        VoiceException failure = await Assert.ThrowsAsync<VoiceException>(() => FinishAsync(result));
        Assert.Equal("STT", failure.Stage);
        Assert.DoesNotContain(sensitive, failure.Message);
        Assert.Contains(diagnostics, value => value.Contains(eventType, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, value => value.Contains(sensitive, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationStopsReceiveAndCannotReturnLateFinal()
    {
        using var socket = ReadySocket();
        using var cancellation = new CancellationTokenSource();
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(PcmChunk(7)),
            _ => { }, cancellation.Token);
        await socket.NextSentAsync();
        Assert.True((await socket.NextSentAsync()).Commit);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FinishAsync(result));
        socket.Deliver("""{"message_type":"committed_transcript","text":"stale"}""");
        Assert.True(result.IsCanceled);
        Assert.True(socket.CancelledReceiveCount > 0);
    }

    [Fact]
    public async Task IgnoresZeroLengthAudioInsteadOfSendingEmptyNonCommitChunks()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket,
            CompletedAudio([], PcmChunk(8), []), _ => { }, CancellationToken.None);
        SentAudio sent = await socket.NextSentAsync();
        Assert.False(sent.Commit);
        Assert.Equal(3_200, sent.Audio.Length);
        Assert.True((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"ok"}""");

        Assert.Equal("ok", await FinishAsync(result));
        Assert.Equal(2, socket.SentCount);
    }

    [Fact]
    public async Task RejectsPartialPcm16SampleWithoutSendingIt()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        byte[] malformed = [1, 2, 3];
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(malformed),
            _ => { }, CancellationToken.None);

        await Assert.ThrowsAsync<VoiceException>(() => FinishAsync(result));
        Assert.Equal(0, socket.SentCount);
        Assert.All(malformed, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public async Task EmptyRecordingFailsBeforeCommit()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        SttAudioSummary? summary = null;
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(),
            _ => { }, CancellationToken.None, value => summary = value);

        VoiceException failure = await Assert.ThrowsAsync<VoiceException>(() => FinishAsync(result));

        Assert.Equal("Microphone", failure.Stage);
        Assert.Equal("NoMicrophoneAudio", failure.Reason);
        Assert.Equal(0, socket.SentCount);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.ChunksSent);
        Assert.Equal(0, summary.BytesSent);
    }

    [Fact]
    public async Task EarlyAutomaticCommitRetainsTextAndKeepsStreamingUntilManualCommit()
    {
        using var socket = ReadySocket();
        var audio = Channel.CreateUnbounded<byte[]>();
        var earlyCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new ElevenLabsSpeechToText(message =>
        {
            if (message.StartsWith("SttEarlyCommit", StringComparison.Ordinal)) earlyCommit.TrySetResult();
        });
        Task<string?> result = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            _ => { }, CancellationToken.None);
        await audio.Writer.WriteAsync(PcmChunk(9));
        Assert.False((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"How do I"}""");
        await earlyCommit.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.IsCompleted);
        Assert.Equal(0, socket.CloseOutputCount);
        await audio.Writer.WriteAsync(PcmChunk(10));
        SentAudio next = await socket.NextSentAsync();
        Assert.False(next.Commit);
        Assert.All(next.Audio, value => Assert.Equal((byte)10, value));
        audio.Writer.Complete();
        Assert.True((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"create a PivotTable?"}""");

        Assert.Equal("How do I create a PivotTable?", await FinishAsync(result));
        Assert.Equal(3, socket.SentCount);
    }

    [Theory]
    [InlineData(48_000, "pcm_16000")]
    [InlineData(16_000, "pcm_48000")]
    public async Task MismatchedServerAudioFormatFailsBeforeTransmittingAudio(int sampleRate, string format)
    {
        using var socket = new FakeWebSocket();
        socket.Deliver(JsonSerializer.Serialize(new
        {
            message_type = "session_started",
            config = new { sample_rate = sampleRate, audio_format = format }
        }));
        using var provider = new ElevenLabsSpeechToText();
        Task<string?> result = provider.TranscribeConnectedAsync(socket, CompletedAudio(PcmChunk(11)),
            _ => { }, CancellationToken.None);

        VoiceException failure = await Assert.ThrowsAsync<VoiceException>(() => FinishAsync(result));

        Assert.Equal("STT", failure.Stage);
        Assert.Equal("UnexpectedServerAudioFormat", failure.Reason);
        Assert.Equal(0, socket.SentCount);
    }

    [Fact]
    public async Task VadReceivesMultipleSegmentsIgnoresTimestampCopiesAndKeepsStreaming()
    {
        using var socket = ReadySocket();
        using var provider = new ElevenLabsSpeechToText();
        using var cancel = new CancellationTokenSource();
        var audio = Channel.CreateUnbounded<byte[]>();
        var segments = Channel.CreateUnbounded<SpeechSegment>();
        Task<string?> run = provider.TranscribeConnectedAsync(socket, audio.Reader.ReadAllAsync(),
            _ => { }, cancel.Token, onCommitted: segment => segments.Writer.TryWrite(segment));
        await audio.Writer.WriteAsync(PcmChunk(3));
        Assert.False((await socket.NextSentAsync()).Commit);
        socket.Deliver("""{"message_type":"committed_transcript","text":"same"}""");
        socket.Deliver("""{"message_type":"committed_transcript_with_timestamps","text":"same"}""");
        socket.Deliver("""{"message_type":"committed_transcript","text":"same"}""");
        Assert.Equal(new SpeechSegment(1, "same"), await segments.Reader.ReadAsync());
        Assert.Equal(new SpeechSegment(2, "same"), await segments.Reader.ReadAsync());
        Assert.False(run.IsCompleted);
        await audio.Writer.WriteAsync(PcmChunk(4));
        Assert.False((await socket.NextSentAsync()).Commit);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FinishAsync(run));
        Assert.Equal(2, socket.SentCount);
    }

    private static FakeWebSocket ReadySocket()
    {
        var socket = new FakeWebSocket();
        socket.Deliver("""{"message_type":"session_started","session_id":"test","config":{"sample_rate":16000,"audio_format":"pcm_16000"}}""");
        return socket;
    }

    private static byte[] PcmChunk(byte value) => Enumerable.Repeat(value, 3_200).ToArray();

    private static async IAsyncEnumerable<byte[]> CompletedAudio(params byte[][] chunks)
    {
        foreach (byte[] chunk in chunks) yield return chunk;
        await Task.CompletedTask;
    }

    private static Task<string?> FinishAsync(Task<string?> task) => task.WaitAsync(TimeSpan.FromSeconds(2));

    private sealed record SentAudio(string? MessageType, int SampleRate, bool Commit, byte[] Audio);

    private sealed class FakeWebSocket : WebSocket
    {
        private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
        private readonly Channel<SentAudio> _sent = Channel.CreateUnbounded<SentAudio>();
        private WebSocketState _state = WebSocketState.Open;
        private int _sentCount;
        private int _cancelledReceiveCount;
        private int _closeOutputCount;

        public int SentCount => Volatile.Read(ref _sentCount);
        public int CancelledReceiveCount => Volatile.Read(ref _cancelledReceiveCount);
        public int CloseOutputCount => Volatile.Read(ref _closeOutputCount);
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override WebSocketState State => _state;

        public void Deliver(string json) => _incoming.Writer.TryWrite(Encoding.UTF8.GetBytes(json));

        public async Task<SentAudio> NextSentAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await _sent.Reader.ReadAsync(timeout.Token);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(WebSocketMessageType.Text, messageType);
            Assert.True(endOfMessage);
            using JsonDocument json = JsonDocument.Parse(buffer.AsMemory());
            JsonElement root = json.RootElement;
            var sent = new SentAudio(root.GetProperty("message_type").GetString(),
                root.GetProperty("sample_rate").GetInt32(), root.GetProperty("commit").GetBoolean(),
                Convert.FromBase64String(root.GetProperty("audio_base_64").GetString()!));
            Interlocked.Increment(ref _sentCount);
            Assert.True(_sent.Writer.TryWrite(sent));
            return Task.CompletedTask;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            byte[] message;
            try { message = await _incoming.Reader.ReadAsync(cancellationToken); }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _cancelledReceiveCount);
                throw;
            }
            Assert.True(message.Length <= buffer.Count);
            message.AsSpan().CopyTo(buffer.AsSpan());
            return new WebSocketReceiveResult(message.Length, WebSocketMessageType.Text, true);
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus,
            string? statusDescription, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _closeOutputCount);
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus,
            string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override void Abort() => _state = WebSocketState.Aborted;
        public override void Dispose() => _state = WebSocketState.Closed;
    }
}
