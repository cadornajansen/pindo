using System.Diagnostics;
using Pointly.App.Automation;
using System.Windows.Automation;
using Pointly.App.Walkthrough;

namespace Pointly.App.Verification;

public sealed record UiControlState(bool Exists, bool? IsSelected, string? Value,
    bool? HasKeyboardFocus = null);

public interface IUiStateVerifier
{
    Task<bool> WaitForAsync(Func<nint> foregroundWindow, ExpectedUiState expected,
        CancellationToken cancellationToken);
}

/// <summary>Reads only the named/identified UIA control; never returns values to logs.</summary>
public sealed class UiAutomationStateVerifier : IUiStateVerifier
{
    public const int PollIntervalMs = 150;
    public const int PollTimeoutMs = 2_000;
    private readonly Func<nint, ExpectedUiState, UiControlState?> _readState;
    private readonly int _pollIntervalMs;
    private readonly int _pollTimeoutMs;

    public UiAutomationStateVerifier(
        Func<nint, ExpectedUiState, UiControlState?>? readState = null,
        int pollIntervalMs = PollIntervalMs, int pollTimeoutMs = PollTimeoutMs)
    {
        if (pollIntervalMs <= 0 || pollTimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(pollIntervalMs));
        _readState = readState ?? ReadState;
        _pollIntervalMs = pollIntervalMs;
        _pollTimeoutMs = pollTimeoutMs;
    }

    public async Task<bool> WaitForAsync(Func<nint> foregroundWindow, ExpectedUiState expected,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            nint hwnd = foregroundWindow();
            if (hwnd == 0) return false;
            UiControlState? snapshot = await UiaWorkScheduler.Shared.RunAsync(() => _readState(hwnd, expected), cancellationToken, _pollTimeoutMs);
            if (snapshot is not null && Matches(expected, snapshot)) return true;
            if (elapsed.ElapsedMilliseconds >= _pollTimeoutMs) return false;
            await Task.Delay(_pollIntervalMs, cancellationToken);
        } while (true);
    }

    public Task<string?> ReadTargetValueAsync(nint hwnd, int[] runtimeId,
        CancellationToken cancellationToken, bool requireFocus = false) =>
        UiaWorkScheduler.Shared.RunAsync(() =>
        {
            AutomationElement? element = FindByRuntimeId(hwnd, runtimeId, cancellationToken);
            if (requireFocus && element?.Current.HasKeyboardFocus != true) return null;
            return ReadValue(element);
        }, cancellationToken);

    public static bool Matches(ExpectedUiState expected, UiControlState actual) => expected.Type switch
    {
        ExpectedUiStateType.ControlExists => actual.Exists,
        ExpectedUiStateType.ControlMissing => !actual.Exists,
        ExpectedUiStateType.IsSelected => actual.Exists && actual.IsSelected == true,
        ExpectedUiStateType.HasKeyboardFocus => actual.Exists && actual.HasKeyboardFocus == true,
        ExpectedUiStateType.ValueEquals => actual.Exists && actual.Value is not null &&
            string.Equals(actual.Value, expected.ExpectedValue, StringComparison.Ordinal),
        ExpectedUiStateType.ValueNonEmpty => actual.Exists && !string.IsNullOrWhiteSpace(actual.Value),
        _ => false,
    };

    private static UiControlState? ReadState(nint hwnd, ExpectedUiState expected)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            Condition condition = !string.IsNullOrWhiteSpace(expected.AutomationId)
                ? new PropertyCondition(AutomationElement.AutomationIdProperty, expected.AutomationId)
                : new PropertyCondition(AutomationElement.NameProperty, expected.Name);
            AutomationElement? element = root.FindFirst(TreeScope.Descendants, condition);
            if (element is null) return new UiControlState(false, null, null);
            bool? selected = expected.Type == ExpectedUiStateType.IsSelected &&
                element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object pattern)
                ? ((SelectionItemPattern)pattern).Current.IsSelected : null;
            bool? focused = expected.Type == ExpectedUiStateType.HasKeyboardFocus
                ? element.Current.HasKeyboardFocus : null;
            string? value = expected.Type is ExpectedUiStateType.ValueEquals or ExpectedUiStateType.ValueNonEmpty
                ? ReadValue(element) : null;
            return new UiControlState(true, selected, value, focused);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static AutomationElement? FindByRuntimeId(nint hwnd, int[] runtimeId,
        CancellationToken cancellationToken)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            int examined = 0;
            foreach (AutomationElement element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (examined++ >= 2_000) break;
                try { if (element.GetRuntimeId().SequenceEqual(runtimeId)) return element; }
                catch (ElementNotAvailableException) { }
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        return null;
    }

    private static string? ReadValue(AutomationElement? element)
    {
        try
        {
            if (element is null) return null;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                return ((ValuePattern)valuePattern).Current.Value;
            return element.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern)
                ? ((TextPattern)textPattern).DocumentRange.GetText(-1) : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
