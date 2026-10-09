using System.Runtime.InteropServices;
using System.Windows;
using Pointly.App.Interop;

namespace Pointly.App.Presentation;

internal sealed record MonitorGeometry(nint Handle, Rect Bounds, Rect WorkArea);

internal static class DesktopGeometry
{
    internal static Point Cursor()
    {
        GetCursorPos(out NativePoint point);
        return new(point.X, point.Y);
    }

    internal static MonitorGeometry At(Point point)
    {
        nint handle = MonitorFromPoint(new NativePoint { X = (int)point.X, Y = (int)point.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(handle, ref info)) throw new InvalidOperationException("MonitorUnavailable");
        return new(handle, Rect(info.Bounds), Rect(info.Work));
    }

    internal static Rect Rect(NativeMethods.RECT rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    {
        public int Size;
        public NativeMethods.RECT Bounds;
        public NativeMethods.RECT Work;
        public uint Flags;
    }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
}
