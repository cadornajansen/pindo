using Pointly.App.Verification;
using Pointly.App.Walkthrough;

namespace Pointly.Tests;

public sealed class ActionVerificationTests
{
    [Fact]
    public void ForegroundChangePausesKeyboardVerificationWithoutCancelling()
    {
        Assert.Equal(InputForegroundDisposition.Observe,
            InputForegroundPolicy.Decide(true, 10, 10));
        Assert.Equal(InputForegroundDisposition.Ignore,
            InputForegroundPolicy.Decide(true, 10, 20));
        Assert.Equal(InputForegroundDisposition.Cancel,
            InputForegroundPolicy.Decide(false, 10, 20));
    }

    [Fact]
    public void KeyboardExpectedKeyCompletesAndWrongKeyDoesNot()
    {
        var state = new KeyboardVerificationState();
        long id = state.Start(0x0D);
        Assert.Equal(StepVerificationStatus.MissedTarget, state.RecordKey(id, 0x09));
        Assert.Equal(id, state.ActiveSessionId);
        Assert.Equal(StepVerificationStatus.Completed, state.RecordKey(id, 0x0D));
        Assert.Null(state.RecordKey(id, 0x0D));
    }

    [Fact]
    public void KeyboardCancellationAndReplacementRejectStaleKeys()
    {
        var state = new KeyboardVerificationState();
        long old = state.Start(0x0D);
        long current = state.Start(0x09);
        Assert.Null(state.RecordKey(old, 0x0D));
        Assert.True(state.Cancel(current));
        Assert.Null(state.RecordKey(current, 0x09));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("  ", null, false)]
    [InlineData("hello", null, true)]
    [InlineData("A1", "A1", true)]
    [InlineData("B2", "A1", false)]
    public void TextEntryMatchesOnlyExpectedValue(string? value, string? expected, bool match) =>
        Assert.Equal(match, TextEntryMatcher.Matches(value, expected));

    [Fact]
    public void TextEntryStateRejectsEmptyAndWrongThenCompletes()
    {
        var state = new TextEntryVerificationState();
        long id = state.Start("PivotTable");
        Assert.Equal(StepVerificationStatus.MissedTarget, state.RecordObservedValue(id, ""));
        Assert.Equal(StepVerificationStatus.MissedTarget, state.RecordObservedValue(id, "Other"));
        Assert.Equal(StepVerificationStatus.Completed, state.RecordObservedValue(id, "PivotTable"));
        Assert.Null(state.RecordObservedValue(id, "PivotTable"));
    }

    [Fact]
    public void TextEntryCancellationAndReplacementRejectStaleValues()
    {
        var state = new TextEntryVerificationState();
        long old = state.Start(null);
        long current = state.Start("A1");
        Assert.Null(state.RecordObservedValue(old, "secret"));
        Assert.True(state.Cancel(current));
        Assert.Null(state.RecordObservedValue(current, "A1"));
    }

    [Fact]
    public void UiStateMatchesExistenceSelectionAndValues()
    {
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ControlExists, Name: "target"), new(true, null, null)));
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ControlMissing, Name: "target"), new(false, null, null)));
        Assert.False(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ControlMissing, Name: "target"), new(true, null, null)));
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.IsSelected, Name: "target"), new(true, true, null)));
        Assert.False(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.IsSelected, Name: "target"), new(true, false, null)));
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.HasKeyboardFocus, Name: "target"), new(true, null, null, true)));
        Assert.False(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.HasKeyboardFocus, Name: "target"), new(true, null, null, false)));
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ValueEquals, Name: "target", ExpectedValue: "A1"),
            new(true, null, "A1")));
        Assert.False(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ValueEquals, Name: "target", ExpectedValue: "A1"),
            new(true, null, "B2")));
        Assert.True(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ValueNonEmpty, Name: "target"), new(true, null, "value")));
        Assert.False(UiAutomationStateVerifier.Matches(
            new(ExpectedUiStateType.ValueNonEmpty, Name: "target"), new(true, null, "")));
    }

    [Fact]
    public async Task UiStateWaitTimesOutWithoutMatching()
    {
        var verifier = new UiAutomationStateVerifier((_, _) => new UiControlState(false, null, null),
            pollIntervalMs: 2, pollTimeoutMs: 15);
        bool matched = await verifier.WaitForAsync(() => (nint)1,
            new(ExpectedUiStateType.ControlExists, Name: "target"), CancellationToken.None);
        Assert.False(matched);
    }

    [Fact]
    public async Task UiStateWaitHonorsCancellation()
    {
        var verifier = new UiAutomationStateVerifier((_, _) => new UiControlState(false, null, null),
            pollIntervalMs: 2, pollTimeoutMs: 100);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifier.WaitForAsync(
            () => (nint)1, new(ExpectedUiStateType.ControlExists, Name: "target"), cancelled.Token));
    }

    [Fact]
    public void MixedActionWalkthroughAdvancesOnlyAfterEachVerifiedStep()
    {
        var definition = new WalkthroughDefinition("mixed", "Mixed",
        [
            new("click", "Click it", "Find it"),
            new("text", "Type text", "Find input", WalkthroughActionType.TextEntry, ExpectedText: "A1"),
            new("key", "Press Enter", null, WalkthroughActionType.KeyPress, ExpectedVirtualKey: 0x0D,
                PostActionState: new(ExpectedUiStateType.ValueEquals, Name: "Name Box", ExpectedValue: "A1")),
        ]);
        var service = new WalkthroughService();
        long id = service.Start(definition).SessionId;
        Assert.Equal(WalkthroughActionType.Click, service.Current?.CurrentStep?.ActionType);
        Assert.Equal(WalkthroughActionType.TextEntry, service.Hit(id, 0)?.CurrentStep?.ActionType);
        Assert.Equal(WalkthroughActionType.KeyPress, service.Hit(id, 1)?.CurrentStep?.ActionType);
        Assert.Null(service.Hit(id, 1));
        Assert.Equal(WalkthroughStatus.Completed, service.Hit(id, 2)?.Status);
    }

    [Fact]
    public async Task FailedPostActionStateStopsWalkthrough()
    {
        var service = new WalkthroughService();
        var definition = new WalkthroughDefinition("one", "One",
            [new("key", "Press Enter", null, WalkthroughActionType.KeyPress, ExpectedVirtualKey: 0x0D,
                PostActionState: new(ExpectedUiStateType.ControlExists, Name: "result"))]);
        long id = service.Start(definition).SessionId;
        var verifier = new UiAutomationStateVerifier((_, _) => new UiControlState(false, null, null),
            pollIntervalMs: 2, pollTimeoutMs: 10);
        Assert.False(await verifier.WaitForAsync(() => (nint)1,
            definition.Steps[0].PostActionState!, CancellationToken.None));
        Assert.Equal(WalkthroughStatus.Failed, service.Fail(id, 0)?.Status);
        Assert.Null(service.Hit(id, 0));
    }
}
