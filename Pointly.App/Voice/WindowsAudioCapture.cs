using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NAudio.Wave;

namespace Pointly.App.Voice;

public sealed class WindowsAudioCapture : IAudioCapture
{
    private WaveInEvent? _input;
    private Channel<byte[]>? _chunks;
    private bool _stopRequested;
    private volatile bool _disposed;
    private readonly object _statisticsLock = new();
    private readonly Action<string>? _diagnostics;
    private long _chunkCount;
    private long _bytesCaptured;
    private long _droppedChunks;
    private double _peakMax;
    private double _weightedRmsSquares;
    private bool _summaryReported;

    public WindowsAudioCapture(Action<string>? diagnostics = null) => _diagnostics = diagnostics;

    public MicrophoneAudioSummary AudioSummary
    {
        get
        {
            lock (_statisticsLock)
                return new(_chunkCount, _bytesCaptured, _peakMax,
                    _bytesCaptured == 0 ? 0 : Math.Sqrt(_weightedRmsSquares / _bytesCaptured), _droppedChunks);
        }
    }

    public void Start()
    {
        if (_input is not null) throw new InvalidOperationException("Microphone is already active.");
        ObjectDisposedException.ThrowIf(_disposed, this);
        (int deviceIndex, string deviceName) = DefaultMicrophone.Resolve();
        Diagnostic($"MicrophoneDevice={deviceName} DeviceIndex={deviceIndex} Selection=WindowsPreferredCapture");
        Diagnostic(DefaultMicrophone.DescribeWindowsDefault());
        _chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        _input = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = SpeechPcmFormat.Create(),
            BufferMilliseconds = SpeechPcmFormat.ChunkMilliseconds
        };
        _input.DataAvailable += OnData;
        _input.RecordingStopped += OnStopped;
        _input.StartRecording();
        SpeechPcmFormat.Validate(_input.WaveFormat);
        Diagnostic("RequestedFormat=16000Hz/16bit/mono ActualFormat=16000Hz/16bit/mono " +
            "Encoding=PCM16LE FormatSource=WinMMNegotiatedOutput ChunkMilliseconds=100 ExpectedChunkBytes=3200");
        if (_stopRequested) _input.StopRecording();
    }

    public async IAsyncEnumerable<byte[]> CaptureAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_chunks is null) throw new InvalidOperationException("Microphone has not started.");
        await foreach (byte[] chunk in _chunks.Reader.ReadAllAsync(cancellationToken))
            yield return chunk;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        Channel<byte[]>? chunks = _chunks;
        if (_disposed || chunks is null) return;
        if (e.BytesRecorded <= 0) return;
        if (e.BytesRecorded % 2 != 0)
        {
            chunks.Writer.TryComplete(new VoiceException("Microphone", "InvalidPcmSampleAlignment"));
            Stop();
            return;
        }
        byte[] bytes = e.Buffer.AsSpan(0, e.BytesRecorded).ToArray();
        (double peak, double rms) = SpeechPcmFormat.Measure(bytes);
        long count;
        lock (_statisticsLock)
        {
            count = ++_chunkCount;
            _bytesCaptured += bytes.Length;
            _peakMax = Math.Max(_peakMax, peak);
            _weightedRmsSquares += rms * rms * bytes.Length;
        }
        if (count <= 3 || count % 20 == 0)
            Diagnostic(FormattableString.Invariant($"MicAudio Chunk={count} Bytes={bytes.Length} Peak={peak:F5} Rms={rms:F5}"));
        if (!chunks.Writer.TryWrite(bytes))
        {
            Array.Clear(bytes);
            if (_disposed) return;
            lock (_statisticsLock) _droppedChunks++;
            // A full channel previously discarded audio silently. Fail explicitly instead of committing an incomplete recording.
            chunks.Writer.TryComplete(new VoiceException("Microphone", "AudioQueueOverflow"));
            Diagnostic("MicAudioFailure=AudioQueueOverflow");
            Stop();
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        // All DataAvailable callbacks have finished. ReadAllAsync drains their queued chunks before ending.
        ReportSummary();
        _chunks?.Writer.TryComplete(e.Exception);
    }

    public void Stop()
    {
        _stopRequested = true;
        if (_input is null) return;
        try { _input.StopRecording(); }
        catch (InvalidOperationException) { _chunks?.Writer.TryComplete(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        if (_input is not null)
        {
            _input.DataAvailable -= OnData;
            _input.RecordingStopped -= OnStopped;
            _input.Dispose();
            _input = null;
        }
        if (_chunks is not null)
        {
            _chunks.Writer.TryComplete();
            while (_chunks.Reader.TryRead(out byte[]? chunk)) Array.Clear(chunk);
            _chunks = null;
        }
        ReportSummary();
    }

    private void ReportSummary()
    {
        lock (_statisticsLock)
        {
            if (_summaryReported) return;
            _summaryReported = true;
        }
        MicrophoneAudioSummary summary = AudioSummary;
        Diagnostic(FormattableString.Invariant($"MicSummary Chunks={summary.Chunks} Bytes={summary.Bytes} PeakMax={summary.PeakMax:F5} RmsAverage={summary.RmsAverage:F5} DroppedChunks={summary.DroppedChunks}"));
    }

    [Conditional("DEBUG")]
    private void Diagnostic(string message) => _diagnostics?.Invoke(message);
}
