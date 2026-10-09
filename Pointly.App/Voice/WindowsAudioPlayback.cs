using System.Diagnostics;
using NAudio.Wave;

namespace Pointly.App.Voice;

public sealed class WindowsAudioPlayback(Action<string>? telemetry = null) : IAudioPlayback
{
    private WaveOutEvent? _output;
    private long _stopRequestedAt;

    public async Task PlayAsync(IAsyncEnumerable<byte[]> audio, Action<long> onFirstAudio,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        var buffer = new BufferedWaveProvider(new WaveFormat(16_000, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(5),
            DiscardOnBufferOverflow = false,
            ReadFully = true
        };
        using var output = new WaveOutEvent { DesiredLatency = 100 };
        _output = output;
        Interlocked.Exchange(ref _stopRequestedAt, 0);
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is null) drained.TrySetResult();
            else drained.TrySetException(new VoiceException("Playback", "DeviceFailed"));
        };
        using var stop = cancellationToken.Register(Stop);
        bool started = false;
        try
        {
            output.Init(buffer);
            await foreach (byte[] chunk in audio.WithCancellation(cancellationToken))
            {
                if (!started)
                {
                    onFirstAudio(elapsed.ElapsedMilliseconds);
                    started = true;
                }
                try
                {
                    while (buffer.BufferedBytes + chunk.Length > buffer.BufferLength)
                        await Task.Delay(25, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    buffer.AddSamples(chunk, 0, chunk.Length);
                }
                finally { Array.Clear(chunk); }
                if (output.PlaybackState != PlaybackState.Playing) output.Play();
            }
            // Return zero at EOF, allowing WaveOut's already queued buffers to play fully.
            buffer.ReadFully = false;
            if (started) await drained.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            output.Stop();
            try
            {
                // A replacement microphone must not open while native playback is still draining/stopping.
                if (started) await drained.Task.WaitAsync(TimeSpan.FromSeconds(2));
                if (Interlocked.Read(ref _stopRequestedAt) is var stoppedAt && stoppedAt != 0)
                    telemetry?.Invoke($"InterruptionToPlaybackStoppedMs={Stopwatch.GetElapsedTime(stoppedAt).TotalMilliseconds:F0}");
            }
            finally
            {
                buffer.ClearBuffer();
                if (ReferenceEquals(_output, output)) _output = null;
            }
        }
    }

    public void Stop()
    {
        Interlocked.CompareExchange(ref _stopRequestedAt, Stopwatch.GetTimestamp(), 0);
        _output?.Stop();
    }
    public void Dispose() => Stop();
}
