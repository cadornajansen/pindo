using Pointly.App.Walkthrough;

namespace Pointly.Tests;

public sealed class WalkthroughTests
{
    private static WalkthroughDefinition Definition(int count = 3) => new("demo", "Demo",
        Enumerable.Range(1, count).Select(index => new WalkthroughStep(
            $"step-{index}", $"Do step {index}", $"Find step {index}")).ToArray());

    [Fact]
    public void StartsAtFirstStepAndMissDoesNotAdvance()
    {
        var service = new WalkthroughService();
        WalkthroughSnapshot start = service.Start(Definition());
        Assert.Equal(WalkthroughStatus.Running, start.Status);
        Assert.Equal("step-1", start.CurrentStep?.Id);
        Assert.Equal(0, service.Miss(start.SessionId, 0)?.StepIndex);
        Assert.Equal(0, service.Current?.StepIndex);
    }

    [Fact]
    public void EachHitAdvancesExactlyOnceAndFinalHitCompletes()
    {
        var service = new WalkthroughService();
        long id = service.Start(Definition(2)).SessionId;
        Assert.Equal("step-2", service.Hit(id, 0)?.CurrentStep?.Id);
        Assert.Null(service.Hit(id, 0));
        Assert.Equal(WalkthroughStatus.Completed, service.Hit(id, 1)?.Status);
        Assert.Null(service.Hit(id, 1));
    }

    [Fact]
    public void ThreeStepsAdvanceInOrder()
    {
        var service = new WalkthroughService();
        long id = service.Start(Definition()).SessionId;
        Assert.Equal(1, service.Hit(id, 0)?.StepIndex);
        Assert.Equal(2, service.Hit(id, 1)?.StepIndex);
        Assert.Equal(WalkthroughStatus.Completed, service.Hit(id, 2)?.Status);
    }

    [Fact]
    public void CancellationPreventsProgression()
    {
        var service = new WalkthroughService();
        long id = service.Start(Definition()).SessionId;
        Assert.Equal(WalkthroughStatus.Cancelled, service.Cancel(id)?.Status);
        Assert.Null(service.Hit(id, 0));
        Assert.Null(service.Miss(id, 0));
    }

    [Fact]
    public void ReplacementRejectsOldSessionAndOldClick()
    {
        var service = new WalkthroughService();
        long old = service.Start(Definition()).SessionId;
        long current = service.Start(Definition()).SessionId;
        Assert.NotEqual(old, current);
        Assert.Null(service.Hit(old, 0));
        Assert.Null(service.Fail(old, 0));
        Assert.Null(service.Cancel(old));
        Assert.Equal(0, service.Current?.StepIndex);
        Assert.Equal(1, service.Hit(current, 0)?.StepIndex);
    }

    [Fact]
    public void ResolutionFailureStopsWalkthrough()
    {
        var service = new WalkthroughService();
        long id = service.Start(Definition()).SessionId;
        Assert.Equal(WalkthroughStatus.Failed, service.Fail(id, 0)?.Status);
        Assert.Null(service.Hit(id, 0));
    }

    [Fact]
    public void EmptyWalkthroughIsRejected()
    {
        var service = new WalkthroughService();
        Assert.Throws<ArgumentException>(() => service.Start(Definition(0)));
    }

    [Fact]
    public void OneStepWalkthroughCompletesOnFirstHit()
    {
        var service = new WalkthroughService();
        long id = service.Start(Definition(1)).SessionId;
        Assert.Equal(WalkthroughStatus.Completed, service.Hit(id, 0)?.Status);
        Assert.Null(service.Current?.CurrentStep);
    }

    [Fact]
    public void DuplicateStepIdsAreRejected()
    {
        var service = new WalkthroughService();
        var step = new WalkthroughStep("same", "Do something", "Find something");
        Assert.Throws<ArgumentException>(() => service.Start(new WalkthroughDefinition(
            "demo", "Demo", [step, step])));
    }
}
