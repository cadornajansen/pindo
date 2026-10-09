using Pointly.App.Tutor;
using Pointly.App.Walkthrough;

namespace Pointly.Tests;

public sealed class GoalSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerificationNeedsEvidenceEvenWhenModelClaimsSuccess(bool matched)
    {
        Assert.Throws<InvalidOperationException>(() => new GoalVerification(matched, "").Validate());
        new GoalVerification(matched, "The dialog shows three columns.").Validate();
    }

    [Fact]
    public void ClarificationKeepsGoalAndVerifiedHistory()
    {
        var goal = new GoalSession();
        goal.Accept("Insert a table with three columns");
        long identity = goal.Identity;
        goal.Completed.Add("Opened Insert");
        goal.AwaitingClarification = true;
        goal.Accept("Two rows");
        Assert.Equal(identity, goal.Identity);
        Assert.Contains("three columns", goal.Goal);
        Assert.Equal(2, goal.Conversation.Count);
        Assert.Single(goal.Completed);
    }

    [Fact]
    public void CancelAndReplacementRejectLateResults()
    {
        var goal = new GoalSession();
        goal.Accept("First");
        long first = goal.Identity;
        goal.Reset();
        Assert.False(goal.IsCurrent(first));
        goal.Accept("Second");
        Assert.False(goal.IsCurrent(first));
        Assert.Empty(goal.Completed);
    }

    [Theory]
    [InlineData(WalkthroughActionType.TextEntry, null, null)]
    [InlineData(WalkthroughActionType.KeyPress, null, 999)]
    public void IncompleteActionIsRejected(WalkthroughActionType action, string? text, int? key)
    {
        var outcome = new PlannerOutcome("plan", "Steps", [new("Do it", "Field", action, "Result", text, key)]);
        Assert.Throws<InvalidOperationException>(outcome.Validate);
    }

    [Fact]
    public void EveryPlannedStepNeedsObservableResult()
    {
        var outcome = new PlannerOutcome("plan", "Steps", [new("Click Insert", "Insert", WalkthroughActionType.Click, "")]);
        Assert.Throws<InvalidOperationException>(outcome.Validate);
    }
}
