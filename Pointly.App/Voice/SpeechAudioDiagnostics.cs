using System.Buffers.Binary;
using NAudio.Wave;

namespace Pointly.App.Voice;

public sealed record MicrophoneAudioSummary(long Chunks, long Bytes, double PeakMax,
    double RmsAverage, long DroppedChunks)
{
    public static readonly MicrophoneAudioSummary Empty = new(0, 0, 0, 0, 0);
}

public sealed record SttAudioSummary(long ChunksSent, long BytesSent)
{
    public static readonly SttAudioSummary Empty = new(0, 0);
}

internal static class SpeechPcmFormat
{
    public const int SampleRate = 16_000;
    public const int BitsPerSample = 16;
    public const int Channels = 1;
    public const int ChunkMilliseconds = 100;
    public const int ChunkBytes = 3_200;
    public const int BytesPerSecond = 32_000;

    public static WaveFormat Create() => new(SampleRate, BitsPerSample, Channels);

    public static void Validate(WaveFormat format)
    {
        if (format.Encoding != WaveFormatEncoding.Pcm || format.SampleRate != SampleRate ||
            format.BitsPerSample != BitsPerSample || format.Channels != Channels ||
            format.BlockAlign != 2 || format.AverageBytesPerSecond != BytesPerSecond)
            throw new VoiceException("Microphone", "UnexpectedPcmFormat");
    }

    public static (double Peak, double Rms) Measure(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length == 0 || pcm.Length % 2 != 0)
            throw new VoiceException("Microphone", "InvalidPcmSampleAlignment");
        double peak = 0;
        double sumSquares = 0;
        for (int offset = 0; offset < pcm.Length; offset += 2)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(offset, 2)) / 32768.0;
            peak = Math.Max(peak, Math.Abs(sample));
            sumSquares += sample * sample;
        }
        return (peak, Math.Sqrt(sumSquares / (pcm.Length / 2)));
    }
}
