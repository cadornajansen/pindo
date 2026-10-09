using System.Diagnostics;
using System.Windows.Threading;
using Pointly.App.Walkthrough;

namespace Pointly.App.Verification;

public enum WalkthroughVerificationStatus { Waiting, Miss, CheckingState, Completed, Failed, Cancelled }

public sealed record WalkthroughVerificationResult(WalkthroughVerificationStatus Status,
    WalkthroughActionType ActionType, long DurationMs, string? Reason = null,
    long StateDurationMs = 0);

/// <summary>Runs exactly one action verifier, then an optional bounded UIA state check.</summary>
public sealed class StepVerificationCoordinator : IDisposable
{
    private readonly ClickVerificationService _click;
    private readonly KeyboardVerificationService _keyboard;
    private readonly TextEntryVerificationService _text;
    private readonly IUiStateVerifier _stateVerifier;
    private long _nextId;
    private long _activeId;
    private WalkthroughActionType _actionType;
    private ExpectedUiState? _expectedState;
    private Func<nint>? _foreground;
    private CancellationTokenSource? _cancellation;
    private Stopwatch? _duration;
    private bool _disposed;

    public event Action<WalkthroughVerificationResult>? ResultChanged;
    public Func<bool>? ContextValid { get; set; }

    public StepVerificationCoordinator(Dispatcher dispatcher, IUiStateVerifier stateVerifier)
    {
        _click = new ClickVerificationService(dispatcher);
        _keyboard = new KeyboardVerificationService(dispatcher);
        _text = new TextEntryVerificationService(dispatcher);
        _stateVerifier = stateVerifier;
        _click.ResultChanged += OnClick;
        _keyboard.ResultChanged += OnKeyboard;
        _text.ResultChanged += OnText;
    }

    public void StartClick(VerifiedClickTarget target, nint hwnd, ExpectedUiState? state,
        Func<nint> foreground, CancellationToken token)
    {
        Begin(WalkthroughActionType.Click, state, foreground, token);
        try { _click.Start(target, hwnd, _cancellation!.Token, ContextValid); }
        catch { Cancel("VerifierStartFailed"); throw; }
    }

    public void StartKey(int virtualKey, nint hwnd, ExpectedUiState? state,
        Func<nint> foreground, CancellationToken token)
    {
        Begin(WalkthroughActionType.KeyPress, state, foreground, token);
        try { _keyboard.Start(virtualKey, hwnd, _cancellation!.Token, ContextValid); }
        catch { Cancel("VerifierStartFailed"); throw; }
    }

    public void StartText(nint hwnd, string? expectedText,
        Func<CancellationToken, Task<string?>> readValue, ExpectedUiState? state,
        Func<nint> foreground, CancellationToken token)
    {
        Begin(WalkthroughActionType.TextEntry, state, foreground, token);
        try { _text.Start(hwnd, expectedText, readValue, _cancellation!.Token, ContextValid); }
        catch { Cancel("VerifierStartFailed"); throw; }
    }

    private void Begin(WalkthroughActionType action, ExpectedUiState? state,
        Func<nint> foreground, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();
        Cancel("ReplacedByNewTarget");
        _activeId = ++_nextId;
        _actionType = action;
        _expectedState = state;
        _foreground = foreground;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _duration = Stopwatch.StartNew();
    }

    private void OnClick(StepVerificationResult result)
    {
        if (_activeId == 0 || _actionType != WalkthroughActionType.Click) return;
        OnAction(result.Status, result.CancellationReason);
    }

    private void OnKeyboard(StepVerificationStatus status, string? reason)
    {
        if (_activeId == 0 || _actionType != WalkthroughActionType.KeyPress) return;
        OnAction(status, reason);
    }

    private void OnText(StepVerificationStatus status, string? reason)
    {
        if (_activeId == 0 || _actionType != WalkthroughActionType.TextEntry) return;
        OnAction(status, reason);
    }

    private void OnAction(StepVerificationStatus status, string? reason)
    {
        switch (status)
        {
            case StepVerificationStatus.Waiting:
                Emit(WalkthroughVerificationStatus.Waiting);
                break;
            case StepVerificationStatus.MissedTarget:
                Emit(WalkthroughVerificationStatus.Miss);
                break;
            case StepVerificationStatus.Completed:
                if (_expectedState is null)
                {
                    Emit(WalkthroughVerificationStatus.Completed);
                    Finish();
                }
                else
                {
                    Emit(WalkthroughVerificationStatus.CheckingState);
                    _ = VerifyStateAsync(_activeId);
                }
                break;
            case StepVerificationStatus.Cancelled:
                Emit(WalkthroughVerificationStatus.Cancelled, reason);
                Finish();
                break;
        }
    }

    private async Task VerifyStateAsync(long id)
    {
        var stateDuration = Stopwatch.StartNew();
        try
        {
            bool matched = await _stateVerifier.WaitForAsync(_foreground!, _expectedState!, _cancellation!.Token);
            if (id != _activeId) return;
            Emit(matched ? WalkthroughVerificationStatus.Completed : WalkthroughVerificationStatus.Failed,
                matched ? null : "UiStateTimeout", stateDuration.ElapsedMilliseconds);
            Finish();
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (id == _activeId)
            {
                Emit(WalkthroughVerificationStatus.Failed, "UiStateUnavailable", stateDuration.ElapsedMilliseconds);
                Finish();
            }
        }
    }

    private void Emit(WalkthroughVerificationStatus status, string? reason = null,
        long stateDurationMs = 0) =>
        ResultChanged?.Invoke(new WalkthroughVerificationResult(status, _actionType,
            _duration?.ElapsedMilliseconds ?? 0, reason, stateDurationMs));

    public void Cancel(string reason)
    {
        if (_activeId == 0) return;
        WalkthroughActionType action = _actionType;
        long duration = _duration?.ElapsedMilliseconds ?? 0;
        Finish();
        _click.Cancel(reason);
        _keyboard.Cancel(reason);
        _text.Cancel(reason);
        ResultChanged?.Invoke(new WalkthroughVerificationResult(
            WalkthroughVerificationStatus.Cancelled, action, duration, reason));
    }

    private void Finish()
    {
        _activeId = 0;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _duration = null;
        _foreground = null;
        _expectedState = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel("ApplicationClosing");
        _click.Dispose();
        _keyboard.Dispose();
        _text.Dispose();
        _disposed = true;
    }
}
