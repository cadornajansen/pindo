using System.Windows.Threading;
using Pointly.App.Interop;
using Pointly.App.Windows;

namespace Pointly.App.Verification;

public sealed class KeyboardVerificationState
{
    private long _nextId;
    private int? _expectedKey;
    public long ActiveSessionId { get; private set; }

    public long Start(int expectedVirtualKey)
    {
        if (expectedVirtualKey is <= 0 or > 255) throw new ArgumentOutOfRangeException(nameof(expectedVirtualKey));
        _expectedKey = expectedVirtualKey;
        return ActiveSessionId = ++_nextId;
    }

    public StepVerificationStatus? RecordKey(long sessionId, int virtualKey)
    {
        if (sessionId == 0 || sessionId != ActiveSessionId || _expectedKey is null) return null;
        if (virtualKey != _expectedKey) return StepVerificationStatus.MissedTarget;
        _expectedKey = null;
        ActiveSessionId = 0;
        return StepVerificationStatus.Completed;
    }

    public bool Cancel(long sessionId)
    {
        if (sessionId == 0 || sessionId != ActiveSessionId) return false;
        _expectedKey = null;
        ActiveSessionId = 0;
        return true;
    }
}

public sealed class KeyboardVerificationService(Dispatcher dispatcher) : IDisposable
{
    private readonly KeyboardVerificationState _state = new();
    private LowLevelKeyboardHook? _hook;
    private CancellationTokenRegistration _registration;
    private nint _expectedHwnd;
    private TargetWindowContext? _context;
    private Func<bool>? _contextValid;
    private uint _expectedProcessId;
    private bool _disposed;

    public event Action<StepVerificationStatus, string?>? ResultChanged;

    public void Start(int expectedVirtualKey, nint expectedHwnd, CancellationToken cancellationToken, Func<bool>? contextValid = null)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedHwnd == 0 || !NativeMethods.IsWindow(expectedHwnd))
            throw new ArgumentException("Target window is unavailable.", nameof(expectedHwnd));
        Cancel("ReplacedByNewTarget");
        long id = _state.Start(expectedVirtualKey);
        _expectedHwnd = expectedHwnd;
        _context = TargetWindowContext.Capture(expectedHwnd);
        _contextValid = contextValid;
        NativeMethods.GetWindowThreadProcessId(expectedHwnd, out _expectedProcessId);
        try
        {
            _hook = new LowLevelKeyboardHook(OnKey);
            _hook.Start();
            _registration = cancellationToken.Register(() =>
                dispatcher.BeginInvoke(() => CancelIfCurrent(id, "RequestCancelled")));
            ResultChanged?.Invoke(StepVerificationStatus.Waiting, null);
        }
        catch
        {
            _state.Cancel(id);
            Stop();
            throw;
        }
    }

    private void OnKey(int virtualKey, nint foregroundHwnd)
    {
        long id = _state.ActiveSessionId;
        if (id == 0) return;
        InputForegroundDisposition disposition = (_context?.IsFresh != true || _contextValid?.Invoke() == false) ? InputForegroundDisposition.Cancel : InputForegroundPolicy.Capture(
            _expectedHwnd, _expectedProcessId, foregroundHwnd);
        dispatcher.BeginInvoke(() =>
        {
            if (id != _state.ActiveSessionId) return;
            if (disposition == InputForegroundDisposition.Cancel)
            {
                Cancel("ForegroundChanged");
                return;
            }
            if (disposition == InputForegroundDisposition.Ignore) return;
            StepVerificationStatus? result = _state.RecordKey(id, virtualKey);
            if (result is null) return;
            if (result == StepVerificationStatus.Completed) Stop();
            ResultChanged?.Invoke(result.Value, null);
        }, DispatcherPriority.Input);
    }

    public void Cancel(string reason)
    {
        dispatcher.VerifyAccess();
        if (!_state.Cancel(_state.ActiveSessionId)) return;
        Stop();
        ResultChanged?.Invoke(StepVerificationStatus.Cancelled, reason);
    }

    private void CancelIfCurrent(long id, string reason)
    {
        if (id == _state.ActiveSessionId) Cancel(reason);
    }

    private void Stop()
    {
        _registration.Dispose();
        _hook?.Dispose();
        _hook = null;
        _expectedHwnd = 0;
        _context = null;
        _contextValid = null;
        _expectedProcessId = 0;
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (_disposed) return;
        Cancel("ApplicationClosing");
        _disposed = true;
    }
}
