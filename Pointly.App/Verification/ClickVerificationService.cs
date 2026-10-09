using System.Diagnostics;
using System.Windows.Threading;
using Pointly.App.Interop;
using Pointly.App.Windows;

namespace Pointly.App.Verification;

/// <summary>Owns one temporary mouse hook and one immutable physical-screen target.</summary>
public sealed class ClickVerificationService(Dispatcher dispatcher) : IDisposable
{
    private readonly ClickVerificationState _state = new();
    private LowLevelMouseHook? _hook;
    private CancellationTokenRegistration _cancellationRegistration;
    private Stopwatch? _duration;
    private ClickTargetType _targetType;
    private nint _expectedHwnd;
    private TargetWindowContext? _context;
    private Func<bool>? _contextValid;
    private bool _disposed;

    public event Action<StepVerificationResult>? ResultChanged;

    public void Start(VerifiedClickTarget target, nint expectedHwnd, CancellationToken cancellationToken,
        Func<bool>? contextValid = null)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedHwnd == 0 || !NativeMethods.IsWindow(expectedHwnd))
            throw new ArgumentException("The target window is no longer available.", nameof(expectedHwnd));

        Cancel("ReplacedByNewTarget");
        long sessionId = _state.Start(target);
        _targetType = target.Type;
        _expectedHwnd = expectedHwnd;
        _context = TargetWindowContext.Capture(expectedHwnd);
        _contextValid = contextValid;
        _duration = Stopwatch.StartNew();
        try
        {
            _hook = new LowLevelMouseHook(OnMouseClick);
            _hook.Start();
            _cancellationRegistration = cancellationToken.Register(() =>
                dispatcher.BeginInvoke(() => CancelIfCurrent(sessionId, "RequestCancelled")));
            ResultChanged?.Invoke(new StepVerificationResult(StepVerificationStatus.Waiting,
                _targetType, 0));
        }
        catch
        {
            _state.Cancel(sessionId);
            StopListening();
            throw;
        }
    }

    private void OnMouseClick(ScreenPoint click, nint foregroundHwnd)
    {
        long sessionId = _state.ActiveSessionId;
        if (sessionId == 0) return;
        // Snapshot freshness before the click reaches Excel; a valid dialog-closing click remains valid later.
        if (_context?.IsFresh != true || _contextValid?.Invoke() == false)
        {
            dispatcher.BeginInvoke(() => CancelIfCurrent(sessionId, "ForegroundChanged"),
                DispatcherPriority.Input);
            return;
        }
        dispatcher.BeginInvoke(() => ProcessClick(sessionId, click, foregroundHwnd),
            DispatcherPriority.Input);
    }

    private void ProcessClick(long sessionId, ScreenPoint click, nint foregroundHwnd)
    {
        if (sessionId != _state.ActiveSessionId) return;
        // The click can close its own dialog before this dispatcher callback runs.
        // The hook captured foregroundHwnd at click time, so compare that snapshot.
        if (foregroundHwnd != _expectedHwnd)
        {
            Cancel("ForegroundChanged");
            return;
        }

        StepVerificationStatus? outcome = _state.RecordClick(sessionId, click);
        if (outcome is null) return;
        long elapsedMs = _duration?.ElapsedMilliseconds ?? 0;
        if (outcome == StepVerificationStatus.Completed) StopListening();
        ResultChanged?.Invoke(new StepVerificationResult(outcome.Value, _targetType, elapsedMs));
    }

    public void Cancel(string reason)
    {
        dispatcher.VerifyAccess();
        long sessionId = _state.ActiveSessionId;
        if (!_state.Cancel(sessionId)) return;
        long elapsedMs = _duration?.ElapsedMilliseconds ?? 0;
        StopListening();
        ResultChanged?.Invoke(new StepVerificationResult(StepVerificationStatus.Cancelled,
            _targetType, elapsedMs, reason));
    }

    private void CancelIfCurrent(long sessionId, string reason)
    {
        if (sessionId == _state.ActiveSessionId) Cancel(reason);
    }

    private void StopListening()
    {
        _cancellationRegistration.Dispose();
        _hook?.Dispose();
        _hook = null;
        _duration = null;
        _expectedHwnd = 0;
        _context = null;
        _contextValid = null;
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (_disposed) return;
        Cancel("ApplicationClosing");
        _disposed = true;
    }
}
