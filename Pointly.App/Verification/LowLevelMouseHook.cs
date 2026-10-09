using System.ComponentModel;
using System.Runtime.InteropServices;
using Pointly.App.Interop;

namespace Pointly.App.Verification;

/// <summary>Observes physical left-button presses without blocking or modifying input.</summary>
internal sealed class LowLevelMouseHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonDoubleClick = 0x0203;
    private const uint LlmhfInjected = 0x00000001;

    private readonly Action<ScreenPoint, nint> _onClick;
    private readonly HookProcedure _callback;
    private nint _hook;

    public LowLevelMouseHook(Action<ScreenPoint, nint> onClick)
    {
        _onClick = onClick;
        _callback = HookCallback;
    }

    public void Start()
    {
        if (_hook != 0) throw new InvalidOperationException("Mouse hook is already installed.");
        _hook = SetWindowsHookEx(WhMouseLl, _callback, GetModuleHandle(null), 0);
        if (_hook == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install the mouse verification hook.");
    }

    private nint HookCallback(int code, nint message, nint data)
    {
        if (code >= 0 && (message == WmLButtonDown || message == WmLButtonDoubleClick))
        {
            MouseHookData mouse = Marshal.PtrToStructure<MouseHookData>(data);
            if ((mouse.Flags & LlmhfInjected) == 0)
            {
                try
                {
                    _onClick(new ScreenPoint(mouse.Position.X, mouse.Position.Y),
                        NativeMethods.GetForegroundWindow());
                }
                catch (Exception)
                {
                    // Never let a managed exception escape a system hook callback.
                }
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook == 0) return;
        nint hook = _hook;
        if (!UnhookWindowsHookEx(hook))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the mouse verification hook.");
        _hook = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint Position;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nint message, nint data);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookType, HookProcedure callback,
        nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
