using Pointly.App.Verification;

namespace Pointly.Tests;

public sealed class ClickVerificationTests
{
    private static readonly VerifiedClickTarget Box = VerifiedClickTarget.ForBox(
        new ScreenRectangle(100, 200, 180, 240));

    [Theory]
    [InlineData(140, 220)]
    [InlineData(100, 200)]
    [InlineData(180, 240)]
    public void BoxIncludesCenterAndEdges(double x, double y) =>
        Assert.True(Box.Contains(new ScreenPoint(x, y)));

    [Theory]
    [InlineData(99, 220)]
    [InlineData(140, 241)]
    public void BoxRejectsOutsideClicks(double x, double y) =>
        Assert.False(Box.Contains(new ScreenPoint(x, y)));

    [Fact]
    public void BoxSupportsNegativeScreenCoordinates()
    {
        VerifiedClickTarget target = VerifiedClickTarget.ForBox(
            new ScreenRectangle(-500, -100, -400, 0));
        Assert.True(target.Contains(new ScreenPoint(-450, -50)));
        Assert.False(target.Contains(new ScreenPoint(450, -50)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 20)]
    [InlineData(18, 24)]
    public void PointIncludesExactInsideAndToleranceEdge(double x, double y)
    {
        VerifiedClickTarget target = VerifiedClickTarget.ForPoint(new ScreenPoint(200, 300));
        Assert.True(target.Contains(new ScreenPoint(200 + x, 300 + y)));
    }

    [Fact]
    public void PointRejectsOutsideTolerance()
    {
        VerifiedClickTarget target = VerifiedClickTarget.ForPoint(new ScreenPoint(-20, 50));
        Assert.False(target.Contains(new ScreenPoint(11, 50)));
    }

    [Fact]
    public void MissKeepsSessionActiveThenHitCompletesOnlyOnce()
    {
        var state = new ClickVerificationState();
        long sessionId = state.Start(Box);

        Assert.Equal(StepVerificationStatus.MissedTarget,
            state.RecordClick(sessionId, new ScreenPoint(20, 20)));
        Assert.Equal(sessionId, state.ActiveSessionId);
        Assert.Equal(StepVerificationStatus.Completed,
            state.RecordClick(sessionId, new ScreenPoint(140, 220)));
        Assert.Null(state.RecordClick(sessionId, new ScreenPoint(140, 220)));
    }

    [Fact]
    public void CancellationPreventsCompletion()
    {
        var state = new ClickVerificationState();
        long sessionId = state.Start(Box);

        Assert.True(state.Cancel(sessionId));
        Assert.Null(state.RecordClick(sessionId, new ScreenPoint(140, 220)));
        Assert.False(state.Cancel(sessionId));
    }

    [Fact]
    public void NewSessionInvalidatesQueuedClickFromOldSession()
    {
        var state = new ClickVerificationState();
        long oldSession = state.Start(Box);
        long newSession = state.Start(VerifiedClickTarget.ForPoint(new ScreenPoint(500, 500)));

        Assert.Null(state.RecordClick(oldSession, new ScreenPoint(140, 220)));
        Assert.False(state.Cancel(oldSession));
        Assert.Equal(StepVerificationStatus.Completed,
            state.RecordClick(newSession, new ScreenPoint(500, 500)));
    }
}
