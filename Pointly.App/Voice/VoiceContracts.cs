namespace Pointly.App.Voice;

public enum VoiceSessionState { Idle, Listening, Finalizing, Processing, Speaking, Cancelled, Failed }

public sealed record VoiceSessionEvent(VoiceSessionState State, string? Detail = null);

public interface IAudioCapture : IDisposable
{
    MicrophoneAudioSummary AudioSummary { get; }
    void Start();
    IAsyncEnumerable<byte[]> CaptureAsync(CancellationToken cancellationToken);
    void Stop();
}

public sealed record SpeechSegment(long Sequence, string Text);
public sealed record VoiceActivity(bool Active, bool MicrophoneOn, bool Processing, bool Speaking, bool Muted);

public interface ISpeechToText : IDisposable
{
    Task ListenAsync(IAsyncEnumerable<byte[]> audio, Action<string> onPartial,
        Action<SpeechSegment> onCommitted, Action<long> onConnected, CancellationToken cancellationToken)
        => throw new NotSupportedException("Persistent STT is unavailable.");
    Task<string?> TranscribeAsync(IAsyncEnumerable<byte[]> audio,
        Action<string> onPartial, Action<long> onConnected, CancellationToken cancellationToken,
        Action<SttAudioSummary>? onAudioSummary = null);
}

public interface ITextToSpeech : IDisposable
{
    string ModelId { get; }
    IAsyncEnumerable<byte[]> StreamAsync(string text, CancellationToken cancellationToken);
}

public interface IAudioPlayback : IDisposable
{
    Task PlayAsync(IAsyncEnumerable<byte[]> audio, Action<long> onFirstAudio,
        CancellationToken cancellationToken);
    void Stop();
}

public interface IVoiceSession : IDisposable
{
    VoiceSessionState State { get; }
    bool IsActive { get; }
    long SessionIdentity { get; }
    VoiceActivity Activity { get; }
    event Action<VoiceActivity>? ActivityChanged;
    void Summon(bool muted = false);
    void Interrupt();
    void SetMuted(bool muted);
    event Action<VoiceSessionEvent>? StateChanged;
    event Action<string>? PartialTranscript;
    event Action<string>? FinalTranscript;
    void Start();
    void StopListening();
    void Cancel(string reason);
    Task SpeakAsync(string instruction, CancellationToken cancellationToken);
}
