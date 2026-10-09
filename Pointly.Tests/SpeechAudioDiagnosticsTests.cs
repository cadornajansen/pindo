using NAudio.Wave;
using Pointly.App.Voice;

namespace Pointly.Tests;

public sealed class SpeechAudioDiagnosticsTests
{
    [Fact]
    public void CaptureContractIsPcm16LittleEndian16KhzMonoWith100MsChunks()
    {
        WaveFormat format = SpeechPcmFormat.Create();
        SpeechPcmFormat.Validate(format);
        Assert.Equal(WaveFormatEncoding.Pcm, format.Encoding);
        Assert.Equal(16_000, format.SampleRate);
        Assert.Equal(16, format.BitsPerSample);
        Assert.Equal(1, format.Channels);
        Assert.Equal(2, format.BlockAlign);
        Assert.Equal(32_000, format.AverageBytesPerSecond);
        Assert.Equal(3_200, SpeechPcmFormat.ChunkBytes);
        Assert.Equal(SpeechPcmFormat.ChunkBytes,
            format.AverageBytesPerSecond * SpeechPcmFormat.ChunkMilliseconds / 1000);
    }

    [Theory]
    [InlineData(48_000, 16, 1)]
    [InlineData(16_000, 16, 2)]
    [InlineData(16_000, 32, 1)]
    public void RejectsMislabeledCaptureFormat(int sampleRate, int bits, int channels)
    {
        Assert.Throws<VoiceException>(() => SpeechPcmFormat.Validate(new WaveFormat(sampleRate, bits, channels)));
    }

    [Fact]
    public void SilenceHasZeroPeakAndRms()
    {
        (double peak, double rms) = SpeechPcmFormat.Measure(new byte[3_200]);
        Assert.Equal(0, peak);
        Assert.Equal(0, rms);
    }

    [Fact]
    public void MeasuresSignedLittleEndianPcmWithoutMutatingAudio()
    {
        // +16384 and -16384, both half full-scale, encoded little-endian.
        byte[] pcm = [0x00, 0x40, 0x00, 0xc0];
        (double peak, double rms) = SpeechPcmFormat.Measure(pcm);
        Assert.Equal(0.5, peak);
        Assert.Equal(0.5, rms);
        Assert.Equal(new byte[] { 0x00, 0x40, 0x00, 0xc0 }, pcm);
    }

    [Fact]
    public void NegativeFullScaleDoesNotOverflow()
    {
        (double peak, double rms) = SpeechPcmFormat.Measure([0x00, 0x80, 0x00, 0x00]);
        Assert.Equal(1, peak);
        Assert.Equal(Math.Sqrt(0.5), rms, 8);
    }

    [Fact]
    public void RejectsIncompleteSample()
    {
        Assert.Throws<VoiceException>(() => SpeechPcmFormat.Measure([0x01]));
    }
}
