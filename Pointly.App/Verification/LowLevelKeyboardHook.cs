using System.ComponentModel;
using System.Runtime.InteropServices;
using Pointly.App.Interop;

namespace Pointly.App.Verification;

/// <summary>Observes real key-down events; never consumes or records text.</summary>
internal sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkHfInjected = 0x10;
    private readonly Action<int, nint> _onKeyDown;
    private readonly HookProcedure _callback;
    private nint _hook;

    public LowLevelKeyboardHook(Action<int, nint> onKeyDown)
    {
        _onKeyDown = onKeyDown;
        _callback = HookCallback;
    }

    public void Start()
    {
        if (_hook != 0) throw new InvalidOperationException("Keyboard hook is already installed.");
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install keyboard verification hook.");
    }

    private nint HookCallback(int code, nint message, nint data)
    {
        if (code >= 0 && (message == WmKeyDown || message == WmSysKeyDown))
        {
            KeyboardHookData key = Marshal.PtrToStructure<KeyboardHookData>(data);
            if ((key.Flags & LlkHfInjected) == 0)
            {
                try { _onKeyDown((int)key.VirtualKey, NativeMethods.GetForegroundWindow()); }
                catch (Exception) { /* A hook must always pass input onward. */ }
            }
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook == 0) return;
        nint hook = _hook;
        _hook = 0;
        if (!UnhookWindowsHookEx(hook)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nint message, nint data);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookType, HookProcedure callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
