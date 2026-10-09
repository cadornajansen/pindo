using System.Windows;
using Pointly.App.Presentation;

namespace Pointly.Tests;

public sealed class PresentationTests
{
    [Theory]
    [InlineData("ControlType.Edit", AnnotationStyle.Underline)]
    [InlineData("ControlType.Pane", AnnotationStyle.Rectangle)]
    [InlineData("ControlType.Button", AnnotationStyle.Pointer)]
    [InlineData(null, AnnotationStyle.Pointer)]
    public void CueComesFromResolvedControlType(string? controlType, AnnotationStyle expected) =>
        Assert.Equal(expected, PresentationGeometry.CueFor(controlType));
    [Theory]
    [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2)]
    public void PlacementAvoidsTargetAndStaysInsideNegativeMonitor(double scale)
    {
        var work = new Rect(-1920, 0, 1920, 1040);
        var target = new Rect(-250, 860, 80, 40);
        BuddyPlacement result = PresentationGeometry.Place(work, target, scale);
        Assert.True(work.Contains(result.Buddy));
        Assert.True(work.Contains(result.Bubble));
        Assert.False(result.Buddy.IntersectsWith(target));
        Assert.False(result.Bubble.IntersectsWith(target));
    }

    [Fact]
    public void ConversionUsesMonitorOriginAndItsOwnScale()
    {
        Assert.Equal(new Point(100, 80), PresentationGeometry.ToDip(new Point(-1770, 120), new Rect(-1920, 0, 1920, 1080), 1.5));
        Assert.Equal(new Point(150, 120), PresentationGeometry.ToDip(new Point(150, 120), new Rect(0, 0, 1920, 1080), 1));
    }

    [Fact]
    public void PresentationDoesNotChangeVerificationBoundsForAnyStyle()
    {
        var bounds = new Rect(20, 30, 80, 25);
        foreach (AnnotationStyle style in Enum.GetValues<AnnotationStyle>())
        {
            var request = new GuidancePresentation(1, "Test", bounds, Style: style);
            PresentationGeometry.Place(new Rect(0, 0, 1920, 1080), bounds, 2);
            Assert.Equal(bounds, request.Target);
        }
    }

    [Fact]
    public void ReplacementAndDismissRejectOldPresentationIdentity()
    {
        var lifetime = new PresentationLifetime();
        long old = lifetime.Replace();
        long current = lifetime.Replace();
        Assert.False(lifetime.IsCurrent(old));
        Assert.True(lifetime.IsCurrent(current));
        lifetime.Dismiss();
        Assert.False(lifetime.IsCurrent(current));
        Assert.False(lifetime.Visible);
    }
}
