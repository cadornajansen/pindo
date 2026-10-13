using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Pindo.App.Input;

internal sealed class ModifierHotkey(Action toggle) : IDisposable
{
    private readonly ModifierChord _chord = new();
    private readonly Action _toggle = toggle;
    private Dispatcher? _dispatcher;
    private HookProcedure? _callback;
    private nint _hook;
    private bool _disposed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dispatcher is not null) throw new InvalidOperationException("Shortcut already started.");
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _callback = OnKeyboard;
                _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
                if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                ResetHeldKeys();
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }
            if (failure is null) Dispatcher.Run();
        }) { IsBackground = true, Name = "Pindo shortcut" };
        thread.Start();
        ready.Wait();
        if (failure is not null) { _dispatcher = null; throw failure; }
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && message is 0x100 or 0x101 or 0x104 or 0x105)
        {
            try
            {
                var key = Marshal.PtrToStructure<KeyboardData>(data);
                ChordResult result = _chord.Update((int)key.Key, message is 0x100 or 0x104, (key.Flags & 0x10) != 0);
                // Mark a standalone Win chord as used without swallowing physical modifier
                // releases. The unassigned mask key has no command; our hook ignores it.
                if (result.MaskStartMenu && !MaskStartMenu()) _chord.Reset();
                else if (result.Toggle) _toggle();
            }
            catch (Exception) { _chord.Reset(); } // Exceptions must never escape a native callback.
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    private static bool MaskStartMenu()
    {
        Input[] input = [new() { Type = 1, Data = new() { Key = new() { VirtualKey = 0xE8 } } },
            new() { Type = 1, Data = new() { Key = new() { VirtualKey = 0xE8, Flags = 2 } } }];
        return SendInput(2, input, Marshal.SizeOf<Input>()) == 2;
    }

    private void ResetHeldKeys() => _chord.Reset(Enumerable.Range(1, 254).Where(key => (GetAsyncKeyState(key) & 0x8000) != 0));
    public void Reset()
    {
        if (_dispatcher is { HasShutdownStarted: false } dispatcher) dispatcher.Invoke(ResetHeldKeys);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_dispatcher is { HasShutdownStarted: false } dispatcher)
            dispatcher.Invoke(() =>
            {
                if (_hook != 0) { UnhookWindowsHookEx(_hook); _hook = 0; }
                _chord.Reset();
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            });
    }

    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint HookProcedure(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyInput Key;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int kind, HookProcedure callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
}
