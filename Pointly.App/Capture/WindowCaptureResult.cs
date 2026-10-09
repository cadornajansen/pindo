using System.Windows;

namespace Pointly.App.Capture;

/// <summary>Per-stage capture latency in milliseconds.</summary>
public sealed record CaptureTimings(
    long InitMs,
    long FrameWaitMs,
    long CopyMs,
    long TotalMs);

/// <summary>
/// One captured window frame. Pixel data is packed BGRA32 (4 bytes per pixel,
/// row stride = <c>PixelWidth * 4</c>), sized for future multimodal use.
/// Coordinates are physical screen pixels, matching UI Automation bounds and
/// the overlay's input space. Kept in memory only; never written to disk.
/// </summary>
public sealed record WindowCaptureResult(
    nint Hwnd,
    int PixelWidth,
    int PixelHeight,
    DateTimeOffset Timestamp,
    byte[] Bgra32,
    Int32Rect WindowRect,
    double DpiScale,
    CaptureTimings Timings);

/// <summary>Expected, diagnosable capture failure (minimized, closed, timeout, ...).</summary>
public sealed class WindowCaptureException : Exception
{
    public WindowCaptureException(string message)
        : base(message)
    {
    }

    public WindowCaptureException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
