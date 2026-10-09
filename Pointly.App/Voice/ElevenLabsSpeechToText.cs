using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;

namespace Pointly.App.Voice;

public sealed class ElevenLabsSpeechToText : ISpeechToText
{
    private readonly string _model = Environment.GetEnvironmentVariable("POINTLY_STT_MODEL") is { Length: > 0 } value
        ? value : "scribe_v2_realtime";
    private readonly Action<string>? _diagnostics;
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    public ElevenLabsSpeechToText(Action<string>? diagnostics = null) => _diagnostics = diagnostics;

    public async Task<string?> TranscribeAsync(IAsyncEnumerable<byte[]> audio,
        Action<string> onPartial, Action<long> onConnected, CancellationToken cancellationToken,
        Action<SttAudioSummary>? onAudioSummary = null)
    {
        return await ConnectAsync(audio, onPartial, onConnected, cancellationToken, onAudioSummary, null);
    }

    public async Task ListenAsync(IAsyncEnumerable<byte[]> audio, Action<string> onPartial,
        Action<SpeechSegment> onCommitted, Action<long> onConnected, CancellationToken cancellationToken)
    {
        await ConnectAsync(audio, onPartial, onConnected, cancellationToken, null, onCommitted);
    }

    private async Task<string?> ConnectAsync(IAsyncEnumerable<byte[]> audio, Action<string> onPartial,
        Action<long> onConnected, CancellationToken cancellationToken,
        Action<SttAudioSummary>? onAudioSummary, Action<SpeechSegment>? onCommitted)
    {
        string key = ElevenLabsCredentials.GetApiKey("STT");
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("xi-api-key", key);
        string url = "wss://api.elevenlabs.io/v1/speech-to-text/realtime" +
            $"?model_id={Uri.EscapeDataString(_model)}&audio_format=pcm_16000&commit_strategy={(onCommitted is null ? "manual" : "vad")}" +
            (onCommitted is null ? "" : "&vad_silence_threshold_secs=1.0&include_timestamps=false");
        var timer = Stopwatch.StartNew();
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connectTimeout.CancelAfter(ResponseTimeout);
            await socket.ConnectAsync(new Uri(url), connectTimeout.Token);
        }
        onConnected(timer.ElapsedMilliseconds);
        return await TranscribeConnectedAsync(socket, audio, onPartial, cancellationToken, onAudioSummary, onCommitted);
    }

    // The socket seam keeps protocol/lifetime tests independent of credentials, microphones and the network.
    internal async Task<string?> TranscribeConnectedAsync(WebSocket socket, IAsyncEnumerable<byte[]> audio,
        Action<string> onPartial, CancellationToken cancellationToken,
        Action<SttAudioSummary>? onAudioSummary = null, Action<SpeechSegment>? onCommitted = null)
    {
        long chunksSent = 0;
        long bytesSent = 0;
        int commitRequested = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<string?> receiveTask = ReceiveFinalAsync(socket, onPartial, started,
            () => Volatile.Read(ref commitRequested) != 0, receiveCancellation.Token, onCommitted);
        Task receiveObserver = CancelInputOnReceiveFailureAsync();
        try
        {
            await started.Task.WaitAsync(ResponseTimeout, cancellationToken);
            try
            {
                await foreach (byte[] chunk in audio.WithCancellation(inputCancellation.Token))
                {
                    try
                    {
                        if (chunk.Length == 0) continue;
                        if (chunk.Length % 2 != 0 || chunk.Length > SpeechPcmFormat.BytesPerSecond)
                            throw new VoiceException("STT", "InvalidPcmChunk");
                        await SendAudioAsync(socket, chunk, false, inputCancellation.Token);
                        chunksSent++;
                        bytesSent += chunk.Length;
                        if (chunksSent <= 3 || chunksSent % 20 == 0)
                            Diagnostic($"SttAudioChunkSent ChunkCount={chunksSent} Bytes={chunk.Length} TotalBytes={bytesSent}");
                    }
                    finally { Array.Clear(chunk); }
                }
            }
            catch (OperationCanceledException) when (receiveTask.IsFaulted && !cancellationToken.IsCancellationRequested)
            {
                return await receiveTask;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (chunksSent == 0) throw new VoiceException("Microphone", "NoMicrophoneAudio");

            if (onCommitted is not null)
                throw new VoiceException("Microphone", "CaptureEndedUnexpectedly");
            // RecordingStopped completes the capture channel. Only after its tail drains do we commit.
            Volatile.Write(ref commitRequested, 1);
            await SendAudioAsync(socket, [], true, cancellationToken);
            Diagnostic($"SttCommitSent QueuedAudioDrained=true ChunksSent={chunksSent} BytesSent={bytesSent}");
            return await receiveTask.WaitAsync(ResponseTimeout, cancellationToken);
        }
        finally
        {
            receiveCancellation.Cancel();
            try { await receiveTask; }
            catch (Exception) when (receiveCancellation.IsCancellationRequested) { }
            await receiveObserver;
            var summary = new SttAudioSummary(chunksSent, bytesSent);
            onAudioSummary?.Invoke(summary);
            Diagnostic($"SttAudioSummary ChunksSent={chunksSent} BytesSent={bytesSent}");
            if (socket.State == WebSocketState.Open)
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", closeTimeout.Token); }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { socket.Abort(); }
            }
        }

        async Task CancelInputOnReceiveFailureAsync()
        {
            try { await receiveTask; }
            catch (Exception) { inputCancellation.Cancel(); }
        }
    }

    private static async Task SendAudioAsync(WebSocket socket, byte[] audio, bool commit,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            message_type = "input_audio_chunk",
            audio_base_64 = Convert.ToBase64String(audio),
            sample_rate = SpeechPcmFormat.SampleRate,
            commit
        });
        try { await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Text, true, cancellationToken); }
        finally { Array.Clear(payload); }
    }

    private async Task<string?> ReceiveFinalAsync(WebSocket socket, Action<string> onPartial,
        TaskCompletionSource started, Func<bool> commitRequested, CancellationToken cancellationToken, Action<SpeechSegment>? onCommitted)
    {
        var buffer = new byte[16_384];
        using var message = new MemoryStream();
        var committedSegments = new List<string>();
        int committedCharacters = 0;
        long segmentSequence = 0;
        try
        {
            while (true)
            {
                ValueWebSocketReceiveResult frame = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (frame.MessageType == WebSocketMessageType.Close)
                    throw new VoiceException("STT", "SocketClosedBeforeFinal");
                if (frame.MessageType != WebSocketMessageType.Text)
                    throw new VoiceException("STT", "UnexpectedMessageType");
                if (message.Length + frame.Count > 32_768)
                    throw new VoiceException("STT", "OversizedProviderMessage");
                message.Write(buffer, 0, frame.Count);
                if (!frame.EndOfMessage) continue;
                using JsonDocument json = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                JsonElement root = json.RootElement;
                string? type = ReadText(root, "message_type");
                switch (type)
                {
                    case "session_started":
                        LogSessionConfiguration(root);
                        started.TrySetResult();
                        break;
                    case "warning":
                        string warning = ReadText(root, "warning") ?? "";
                        string code = warning.Contains("retention", StringComparison.OrdinalIgnoreCase)
                            ? "RetentionWarning" : "ProviderWarning";
                        Diagnostic($"SttServerEvent=warning Code={code}");
                        break;
                    case "partial_transcript":
                    case "committed_transcript":
                        string text = ReadText(root, "text") ?? throw new VoiceException("STT", "InvalidTranscriptMessage");
                        Diagnostic($"SttServerEvent={type} CharacterCount={text.Length}");
                        if (type == "partial_transcript") onPartial(text);
                        else
                        {
                            if (onCommitted is not null)
                            {
                                onCommitted(new SpeechSegment(++segmentSequence, text));
                                break;
                            }
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                committedCharacters += text.Length;
                                if (committedCharacters > 32_768) throw new VoiceException("STT", "OversizedTranscript");
                                committedSegments.Add(text.Trim());
                            }
                            if (commitRequested()) return string.Join(" ", committedSegments);
                            // Manual mode may still auto-commit long utterances. Keep receiving/streaming until the user stops.
                            Diagnostic("SttEarlyCommit Retained=true");
                        }
                        break;
                    case "committed_transcript_with_timestamps":
                        // Enriched copy of an already committed segment, never a second user turn.
                        Diagnostic("SttTimestampSupplementIgnored");
                        break;
                    case "auth_error":
                    case "quota_exceeded":
                    case "input_error":
                    case "invalid_request":
                    case "rate_limited":
                    case "transcriber_error":
                    case "error":
                    case "unaccepted_terms":
                    case "commit_throttled":
                    case "queue_overflow":
                    case "resource_exhausted":
                    case "session_time_limit_exceeded":
                    case "chunk_size_exceeded":
                    case "insufficient_audio_activity":
                        Diagnostic($"SttServerEvent=error Code={type}");
                        throw new VoiceException("STT", type);
                    default:
                        Diagnostic("SttServerEvent=UnrecognizedEvent");
                        break;
                }
                message.SetLength(0);
            }
        }
        catch (Exception ex)
        {
            started.TrySetException(ex);
            throw;
        }
        finally
        {
            Array.Clear(buffer);
            Array.Clear(message.GetBuffer());
        }
    }

    private void LogSessionConfiguration(JsonElement root)
    {
        int? sampleRate = null;
        string? format = null;
        if (root.TryGetProperty("config", out JsonElement config) && config.ValueKind == JsonValueKind.Object)
        {
            if (config.TryGetProperty("sample_rate", out JsonElement sample) &&
                sample.ValueKind == JsonValueKind.Number && sample.TryGetInt32(out int rate)) sampleRate = rate;
            format = ReadText(config, "audio_format");
        }
        Diagnostic($"SttServerEvent=session_started SampleRate={sampleRate?.ToString() ?? "Unreported"} AudioFormat={(format == "pcm_16000" ? "pcm_16000" : format is null ? "Unreported" : "Unexpected")}");
        if ((sampleRate is not null && sampleRate != SpeechPcmFormat.SampleRate) ||
            (format is not null && format != "pcm_16000"))
            throw new VoiceException("STT", "UnexpectedServerAudioFormat");
    }

    private static string? ReadText(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private void Diagnostic(string message) => _diagnostics?.Invoke(message);

    public void Dispose() { }
}

public sealed class VoiceException(string stage, string reason) : Exception(reason)
{
    public string Stage { get; } = stage;
    public string Reason { get; } = reason;
}
