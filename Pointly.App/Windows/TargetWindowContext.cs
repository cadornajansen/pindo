using System.Runtime.InteropServices;
using System.Windows;
using Pointly.App.Interop;

namespace Pointly.App.Windows;

public sealed record TargetWindowContext(nint Hwnd, uint ProcessId, nint RootOwner, Rect Bounds, uint Dpi)
{
    public static TargetWindowContext? Capture(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd) || NativeMethods.IsIconic(hwnd) ||
            !NativeMethods.GetWindowRect(hwnd, out var bounds)) return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId || bounds.Width <= 0 || bounds.Height <= 0) return null;
        return new(hwnd, pid, GetAncestor(hwnd, 3), new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
            NativeMethods.GetDpiForWindow(hwnd));
    }
    public bool IsFresh => Matches(Capture(Hwnd));
    public bool Matches(TargetWindowContext? other) => other is not null && this == other;
    public bool Owns(TargetWindowContext? other) => other is not null && ProcessId == other.ProcessId &&
        RootOwner != 0 && RootOwner == other.RootOwner;
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
}
