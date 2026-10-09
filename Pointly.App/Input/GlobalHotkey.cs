using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Pointly.App.Interop;

namespace Pointly.App.Input;

/// <summary>
/// Registers a system-wide hotkey (Ctrl+Space) on the owning window and
/// raises <see cref="Pressed"/> when Windows delivers WM_HOTKEY.
/// Safe to dispose multiple times; duplicate <see cref="Register"/> calls are ignored.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    public const int HotkeyId = 1;

    private readonly Window _window;
    private readonly int _id;
    private readonly uint _modifiers;
    private readonly string _name;
    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? Pressed;

    public bool IsRegistered => _registered;

    public GlobalHotkey(Window window, int id = HotkeyId, uint modifiers = NativeMethods.MOD_CONTROL, string name = "Ctrl+Space")
    {
        _window = window;
        _id = id;
        _modifiers = modifiers;
        _name = name;
    }

    /// <summary>
    /// Registers Ctrl+Space. Must be called after the window has a handle
    /// (e.g. from <c>OnSourceInitialized</c>). Ignored if already registered.
    /// </summary>
    /// <exception cref="Win32Exception">Registration failed (e.g. hotkey already taken).</exception>
    public void Register()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_registered)
        {
            return;
        }

        _hwnd = new WindowInteropHelper(_window).Handle;
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available. Call Register after OnSourceInitialized.");
        }

        _source = HwndSource.FromHwnd(_hwnd)
            ?? throw new InvalidOperationException("Could not obtain HwndSource for window.");
        _source.AddHook(WndProc);

        if (!NativeMethods.RegisterHotKey(_hwnd, _id, _modifiers | 0x4000, NativeMethods.VK_SPACE))
        {
            int error = Marshal.GetLastWin32Error();
            _source.RemoveHook(WndProc);
            _source = null;
            _hwnd = IntPtr.Zero;
            throw new Win32Exception(error, $"Failed to register {_name}. It may already be in use by another application.");
        }

        _registered = true;
    }

    /// <summary>
    /// Unregisters the hotkey. Safe to call when not registered.
    /// </summary>
    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            NativeMethods.UnregisterHotKey(_hwnd, _id);
        }
        finally
        {
            _registered = false;
            if (_source is not null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
            _hwnd = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _id)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
