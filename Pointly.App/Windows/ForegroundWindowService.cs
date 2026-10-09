using System.Diagnostics;
using System.Text;
using Pointly.App.Interop;

namespace Pointly.App.Windows;

/// <summary>
/// Snapshot of the current foreground window at the time of a hotkey press.
/// </summary>
public sealed record ForegroundWindowInfo(
    nint Hwnd,
    uint ProcessId,
    string ProcessName,
    string WindowTitle, TargetWindowContext? Context = null);

/// <summary>
/// Resolves the current foreground window to HWND + process name + window title.
/// Returns null only when there is no foreground window.
/// </summary>
public sealed class ForegroundWindowService
{
    public ForegroundWindowInfo? TryGetForegroundInfo()
    {
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId) return null;
        string processName = ResolveProcessName(pid);
        string title = GetWindowTitle(hwnd);

        return new ForegroundWindowInfo(hwnd, pid, processName, title, TargetWindowContext.Capture(hwnd));
    }

    private static string ResolveProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch (Exception)
        {
            // Process may have exited between the HWND and PID lookup.
            return "<unknown>";
        }
    }

    private static string GetWindowTitle(nint hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
