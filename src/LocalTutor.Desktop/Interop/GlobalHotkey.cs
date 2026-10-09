using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LocalTutor.Desktop.Interop;

internal sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0x4C54;
    private const int HotkeyMessage = 0x0312;
    private const uint ControlNoRepeat = 0x0002 | 0x4000;
    private const uint SpaceKey = 0x20;
    private readonly HwndSource _source;
    private bool _disposed;

    public bool IsRegistered { get; }
    public int RegistrationError { get; }
    public event Action<nint>? Pressed;

    public GlobalHotkey(HwndSource source)
    {
        _source = source;
        _source.AddHook(HandleMessage);
        IsRegistered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, ControlNoRepeat, SpaceKey);
        RegistrationError = IsRegistered ? 0 : Marshal.GetLastWin32Error();
    }

    private nint HandleMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == HotkeyMessage && wParam.ToInt32() == HotkeyId)
        {
            // Read this before the assistant activates itself.
            nint foregroundWindow = NativeMethods.GetForegroundWindow();
            handled = true;
            Pressed?.Invoke(foregroundWindow);
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        }

        _source.RemoveHook(HandleMessage);
        _disposed = true;
    }
}
