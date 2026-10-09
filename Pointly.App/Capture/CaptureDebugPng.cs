using System.IO;

namespace Pointly.App.Capture;

/// <summary>
/// Writes a captured frame to PNG, strictly opt-in: only when the
/// POINTLY_CAPTURE_DEBUG_DIR environment variable points at a directory.
/// Never called in normal operation — no screenshots hit disk by default.
/// </summary>
internal static class CaptureDebugPng
{
    private const string EnvVar = "POINTLY_CAPTURE_DEBUG_DIR";

    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

    public static string Save(WindowCaptureResult result)
    {
        string dir = Environment.GetEnvironmentVariable(EnvVar)!;
        Directory.CreateDirectory(dir);

        string path = Path.Combine(
            dir, $"pointly-capture-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

        File.WriteAllBytes(path, CapturePngEncoder.Encode(result));
        return path;
    }
}
