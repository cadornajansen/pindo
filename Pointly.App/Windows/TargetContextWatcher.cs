using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Pointly.App.Interop;

namespace Pointly.App.Windows;

/// <summary>Event-driven invalidation only: never captures screens or requests a model.</summary>
public sealed class TargetContextWatcher : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _invalidated;
    private readonly Action<string>? _log;
    private readonly WinEvent _callback;
    private readonly List<nint> _hooks = [];
    private readonly DispatcherTimer _debounce;
    private TargetWindowContext? _context;
    private bool _layoutChanged;
    private bool _trackLayout;
    private long _identity;
    public bool IsValid => _context?.IsFresh == true && !_layoutChanged &&
        NativeMethods.GetForegroundWindow() == _context.Hwnd;

    public TargetContextWatcher(Dispatcher dispatcher, Action<string> invalidated, Action<string>? log = null)
    {
        _dispatcher = dispatcher; _invalidated = invalidated; _callback = OnEvent; _log = log;
        _debounce = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background,
            (_, _) => Validate(), dispatcher);
        _debounce.Stop();
    }

    public void Watch(TargetWindowContext context, bool trackLayout = true)
    {
        Clear();
        _context = context;
        _trackLayout = trackLayout;
        try
        {
            foreach ((uint first, uint last) in new[] { (3u, 3u), (0x16u, 0x17u), (0x8000u, 0x800Bu) })
            {
                nint hook = SetWinEventHook(first, last, 0, _callback, 0, 0, first == 3 ? 0u : 0x0002u);
                if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                _hooks.Add(hook);
            }
        }
        catch { Clear(); throw; }
    }

    private void OnEvent(nint hook, uint kind, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        TargetWindowContext? context = _context;
        if (context is null || (kind != 3 && hwnd != context.Hwnd)) return;
        if (kind != 3 && objectId is not (0 or -4)) return;
        bool layout = _trackLayout && (kind == 0x8004 || (kind == 0x800B && objectId == -4));
        _layoutChanged |= layout; // Disarm immediately; visual invalidation is debounced.
        long identity = _identity;
        _dispatcher.BeginInvoke(() =>
        {
            if (_context is null || identity != _identity) return;
            _layoutChanged |= layout;
            // A bounded delay lets the input-priority verified click transition first.
            if (!_debounce.IsEnabled) _debounce.Start();
        }, DispatcherPriority.Background);
    }

    private void Validate()
    {
        _debounce.Stop();
        if (_context is not { } context) return;
        nint foreground = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundProcess);
        // Composer controls belong to this session; interacting with them is not an app switch.
        if (foregroundProcess == Environment.ProcessId && context.IsFresh && !_layoutChanged) return;
        string? reason = !context.IsFresh ? "WindowGeometryChanged" :
            foreground != context.Hwnd ? "ForegroundChanged" :
            _layoutChanged ? "LayoutChanged" : null;
        _layoutChanged = false;
        if (reason is null) return;
        _log?.Invoke($"TargetContextChanged Reason={reason} ExpectedHwnd={context.Hwnd} ForegroundHwnd={foreground} ExpectedPid={context.ProcessId} ForegroundPid={foregroundProcess} ExpectedBounds={context.Bounds} CurrentBounds={TargetWindowContext.Capture(context.Hwnd)?.Bounds}");
        Clear(); _invalidated(reason);
    }

    public void Clear()
    {
        ++_identity; _context = null; _layoutChanged = false; _debounce.Stop();
        foreach (nint hook in _hooks) UnhookWinEvent(hook);
        _hooks.Clear();
    }
    public void Dispose() => Clear();
    private delegate void WinEvent(nint hook, uint kind, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(
        uint first, uint last, nint module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
}
