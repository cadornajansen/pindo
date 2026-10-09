using System.Windows.Threading;
using Pointly.App.Interop;
using Pointly.App.Windows;

namespace Pointly.App.Verification;

public static class TextEntryMatcher
{
    public static bool Matches(string? value, string? expectedText) => value is not null &&
        (expectedText is null ? !string.IsNullOrWhiteSpace(value) :
            string.Equals(value, expectedText, StringComparison.Ordinal));
}

public sealed class TextEntryVerificationState
{
    private long _nextId;
    private string? _expectedText;
    public long ActiveSessionId { get; private set; }

    public long Start(string? expectedText)
    {
        _expectedText = expectedText;
        return ActiveSessionId = ++_nextId;
    }

    public StepVerificationStatus? RecordObservedValue(long id, string? value)
    {
        if (id == 0 || id != ActiveSessionId) return null;
        if (!TextEntryMatcher.Matches(value, _expectedText)) return StepVerificationStatus.MissedTarget;
        ActiveSessionId = 0;
        _expectedText = null;
        return StepVerificationStatus.Completed;
    }

    public bool Cancel(long id)
    {
        if (id == 0 || id != ActiveSessionId) return false;
        ActiveSessionId = 0;
        _expectedText = null;
        return true;
    }
}

/// <summary>Observes only typing activity; reads the intended UIA control, never the typed keys.</summary>
public sealed class TextEntryVerificationService(Dispatcher dispatcher) : IDisposable
{
    private const int ReadDelayMs = 150;
    private readonly TextEntryVerificationState _state = new();
    private nint _expectedHwnd;
    private TargetWindowContext? _context;
    private Func<bool>? _contextValid;
    private uint _expectedProcessId;
    private Func<CancellationToken, Task<string?>>? _readValue;
    private LowLevelKeyboardHook? _hook;
    private CancellationTokenRegistration _registration;
    private CancellationToken _token;
    private long _checkingId;
    private bool _pending;
    private bool _disposed;

    public event Action<StepVerificationStatus, string?>? ResultChanged;

    public void Start(nint expectedHwnd, string? expectedText,
        Func<CancellationToken, Task<string?>> readValue, CancellationToken cancellationToken, Func<bool>? contextValid = null)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedHwnd == 0 || !NativeMethods.IsWindow(expectedHwnd))
            throw new ArgumentException("Target window is unavailable.", nameof(expectedHwnd));
        ArgumentNullException.ThrowIfNull(readValue);
        Cancel("ReplacedByNewTarget");
        long id = _state.Start(expectedText);
        _expectedHwnd = expectedHwnd;
        _context = TargetWindowContext.Capture(expectedHwnd);
        _contextValid = contextValid;
        NativeMethods.GetWindowThreadProcessId(expectedHwnd, out _expectedProcessId);
        _readValue = readValue;
        _token = cancellationToken;
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
        if (id == 0 || virtualKey is 0x10 or 0x11 or 0x12 or 0x1B) return;
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
            _pending = true;
            if (_checkingId != id) _ = CheckAfterTypingAsync(id);
        }, DispatcherPriority.Input);
    }

    private async Task CheckAfterTypingAsync(long id)
    {
        _checkingId = id;
        CancellationToken token = _token;
        Func<CancellationToken, Task<string?>> readValue = _readValue!;
        try
        {
            while (id == _state.ActiveSessionId && _pending)
            {
                _pending = false;
                await Task.Delay(ReadDelayMs, token);
                if (id != _state.ActiveSessionId) return;
                string? value = await readValue(token);
                if (id != _state.ActiveSessionId) return;
                if (_context?.IsFresh != true || _contextValid?.Invoke() == false) { Cancel("TargetContextChanged"); return; }
                if (NativeMethods.GetForegroundWindow() != _expectedHwnd) return;
                StepVerificationStatus? status = _state.RecordObservedValue(id, value);
                if (status is null) return;
                if (status == StepVerificationStatus.Completed)
                {
                    Stop();
                    ResultChanged?.Invoke(StepVerificationStatus.Completed, null);
                    return;
                }
                ResultChanged?.Invoke(StepVerificationStatus.MissedTarget, null);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (id == _state.ActiveSessionId) Cancel("TargetValueUnavailable");
        }
        finally
        {
            if (_checkingId == id) _checkingId = 0;
            if (_state.ActiveSessionId != 0 && _pending && _checkingId != _state.ActiveSessionId)
                _ = CheckAfterTypingAsync(_state.ActiveSessionId);
        }
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
        _pending = false;
        _registration.Dispose();
        _hook?.Dispose();
        _hook = null;
        _readValue = null;
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
