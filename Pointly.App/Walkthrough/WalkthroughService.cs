using System.Diagnostics;

namespace Pointly.App.Walkthrough;

public enum WalkthroughStatus { Idle, Running, Completed, Cancelled, Failed }

public enum WalkthroughActionType { Click, KeyPress, TextEntry }
public enum WalkthroughTargetKind { Actionable, Input }

public enum ExpectedUiStateType { ControlExists, ControlMissing, IsSelected, HasKeyboardFocus, ValueEquals, ValueNonEmpty }

public sealed record ExpectedUiState(ExpectedUiStateType Type, string? Name = null,
    string? AutomationId = null, string? ExpectedValue = null);

public sealed record WalkthroughStep(string Id, string Instruction, string? GroundingQuery,
    WalkthroughActionType ActionType = WalkthroughActionType.Click,
    int? ExpectedVirtualKey = null, string? ExpectedText = null,
    ExpectedUiState? PostActionState = null,
    WalkthroughTargetKind TargetKind = WalkthroughTargetKind.Actionable);

public sealed record WalkthroughDefinition(string Id, string Title, IReadOnlyList<WalkthroughStep> Steps);

public sealed record WalkthroughSnapshot(long SessionId, WalkthroughStatus Status,
    WalkthroughDefinition Definition, int StepIndex, long TotalDurationMs, long StepDurationMs)
{
    public WalkthroughStep? CurrentStep => Status == WalkthroughStatus.Running
        ? Definition.Steps[StepIndex] : null;
}

/// <summary>Pure one-session state machine. A session ID and step index reject late callbacks.</summary>
public sealed class WalkthroughService
{
    private long _nextSessionId;
    private WalkthroughDefinition? _definition;
    private WalkthroughStatus _status = WalkthroughStatus.Idle;
    private int _stepIndex;
    private Stopwatch? _total;
    private Stopwatch? _step;

    public WalkthroughSnapshot? Current => _definition is null ? null : Snapshot();

    public WalkthroughSnapshot Start(WalkthroughDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Title) ||
            definition.Steps is null || definition.Steps.Count == 0 ||
            definition.Steps.Any(step => string.IsNullOrWhiteSpace(step.Id) ||
                string.IsNullOrWhiteSpace(step.Instruction) ||
                (step.ActionType != WalkthroughActionType.KeyPress && string.IsNullOrWhiteSpace(step.GroundingQuery)) ||
                (step.ActionType == WalkthroughActionType.KeyPress && step.ExpectedVirtualKey is not (> 0 and <= 255)) ||
                (step.PostActionState is { } state && string.IsNullOrWhiteSpace(state.Name) &&
                    string.IsNullOrWhiteSpace(state.AutomationId)) ||
                (step.PostActionState?.Type == ExpectedUiStateType.ValueEquals &&
                    step.PostActionState.ExpectedValue is null)) ||
            definition.Steps.Select(step => step.Id).Distinct(StringComparer.Ordinal).Count() != definition.Steps.Count)
            throw new ArgumentException("A walkthrough needs distinct, complete steps.", nameof(definition));

        _nextSessionId++;
        _definition = definition with { Steps = definition.Steps.ToArray() };
        _status = WalkthroughStatus.Running;
        _stepIndex = 0;
        _total = Stopwatch.StartNew();
        _step = Stopwatch.StartNew();
        return Snapshot();
    }

    public bool IsCurrent(long sessionId, int stepIndex) =>
        _status == WalkthroughStatus.Running && _nextSessionId == sessionId && _stepIndex == stepIndex;

    public WalkthroughSnapshot? Miss(long sessionId, int stepIndex) =>
        IsCurrent(sessionId, stepIndex) ? Snapshot() : null;

    public WalkthroughSnapshot? Hit(long sessionId, int stepIndex)
    {
        if (!IsCurrent(sessionId, stepIndex)) return null;
        _stepIndex++;
        if (_stepIndex == _definition!.Steps.Count)
        {
            _status = WalkthroughStatus.Completed;
            _total?.Stop();
            _step?.Stop();
        }
        else _step = Stopwatch.StartNew();
        return Snapshot();
    }

    public WalkthroughSnapshot? Cancel(long sessionId)
    {
        if (_status != WalkthroughStatus.Running || _nextSessionId != sessionId) return null;
        _status = WalkthroughStatus.Cancelled;
        _total?.Stop();
        _step?.Stop();
        return Snapshot();
    }

    public WalkthroughSnapshot? Fail(long sessionId, int stepIndex)
    {
        if (!IsCurrent(sessionId, stepIndex)) return null;
        _status = WalkthroughStatus.Failed;
        _total?.Stop();
        _step?.Stop();
        return Snapshot();
    }

    private WalkthroughSnapshot Snapshot() => new(_nextSessionId, _status, _definition!,
        _stepIndex, _total?.ElapsedMilliseconds ?? 0, _step?.ElapsedMilliseconds ?? 0);
}

public static class DemoWalkthroughs
{
    public static readonly WalkthroughDefinition ExcelPivotTable = new(
        "excel-pivottable", "Create a PivotTable",
        [
            new("insert-tab", "Open the Insert tab.", "Locate the Insert tab."),
            new("pivot-table", "Select PivotTable.", "Locate the PivotTable control."),
            new("confirm-dialog", "Create the PivotTable.", "Locate the OK button in the Create PivotTable dialog."),
        ]);

    public static readonly WalkthroughDefinition ExcelSearch = new(
        "excel-search", "Search Excel for PivotTable",
        [
            new("search-click", "Click Microsoft search at the top of Excel.",
                "Locate the Microsoft search input at the top of Excel.",
                PostActionState: new(ExpectedUiStateType.HasKeyboardFocus,
                    AutomationId: "TellMeTextBoxAutomationId"),
                TargetKind: WalkthroughTargetKind.Input),
            new("search-entry", "Click Excel search, type exactly PivotTable, then wait for Step 3.",
                "Locate the Microsoft search input at the top of Excel.",
                WalkthroughActionType.TextEntry, ExpectedText: "PivotTable",
                TargetKind: WalkthroughTargetKind.Input),
            new("search-enter", "With Excel search focused, press Enter.", null,
                WalkthroughActionType.KeyPress, ExpectedVirtualKey: 0x0D),
        ]);
}
