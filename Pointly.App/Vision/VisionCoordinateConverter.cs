using System.Windows;
using Pointly.App.Capture;

namespace Pointly.App.Vision;

public sealed record VisionCoordinateResult(
    Int32Rect ScreenshotPixels,
    Rect PhysicalScreenBounds);

public static class VisionCoordinateConverter
{
    public static VisionCoordinateResult ToPhysicalScreen(
        NormalizedGroundingPoint point,
        WindowCaptureResult capture)
    {
        if (capture.PixelWidth <= 0 || capture.PixelHeight <= 0 ||
            capture.WindowRect.Width <= 0 || capture.WindowRect.Height <= 0)
            throw new ArgumentException("Capture dimensions must be positive.", nameof(capture));

        int x = Math.Clamp((int)Math.Round(point.X * capture.PixelWidth / 1_000.0), 0, capture.PixelWidth - 1);
        int y = Math.Clamp((int)Math.Round(point.Y * capture.PixelHeight / 1_000.0), 0, capture.PixelHeight - 1);
        double screenX = capture.WindowRect.X + x * capture.WindowRect.Width / (double)capture.PixelWidth;
        double screenY = capture.WindowRect.Y + y * capture.WindowRect.Height / (double)capture.PixelHeight;
        const double ringDiameter = 28;
        return new VisionCoordinateResult(
            new Int32Rect(x, y, 1, 1),
            new Rect(screenX - ringDiameter / 2, screenY - ringDiameter / 2, ringDiameter, ringDiameter));
    }

    public static VisionCoordinateResult ToPhysicalScreen(
        NormalizedBoundingBox normalized,
        WindowCaptureResult capture)
    {
        if (capture.PixelWidth <= 0 || capture.PixelHeight <= 0 ||
            capture.WindowRect.Width <= 0 || capture.WindowRect.Height <= 0)
        {
            throw new ArgumentException("Capture dimensions must be positive.", nameof(capture));
        }

        int left = Math.Clamp((int)Math.Floor(normalized.X1 * capture.PixelWidth / 1_000.0), 0, capture.PixelWidth - 1);
        int top = Math.Clamp((int)Math.Floor(normalized.Y1 * capture.PixelHeight / 1_000.0), 0, capture.PixelHeight - 1);
        int right = Math.Clamp((int)Math.Ceiling(normalized.X2 * capture.PixelWidth / 1_000.0), left + 1, capture.PixelWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(normalized.Y2 * capture.PixelHeight / 1_000.0), top + 1, capture.PixelHeight);
        var screenshotPixels = new Int32Rect(left, top, right - left, bottom - top);

        // D3's capture rectangle is in physical screen pixels. Ratio conversion
        // also handles a capture surface whose pixel dimensions differ slightly.
        double scaleX = capture.WindowRect.Width / (double)capture.PixelWidth;
        double scaleY = capture.WindowRect.Height / (double)capture.PixelHeight;
        var physical = new Rect(
            capture.WindowRect.X + left * scaleX,
            capture.WindowRect.Y + top * scaleY,
            (right - left) * scaleX,
            (bottom - top) * scaleY);

        return new VisionCoordinateResult(screenshotPixels, physical);
    }
}
