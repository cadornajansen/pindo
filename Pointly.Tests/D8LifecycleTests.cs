using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pointly.App.Automation;
using Pointly.App.Presentation;
using Pointly.App.Windows;

namespace Pointly.Tests;

public sealed class D8LifecycleTests
{
    [Fact]
    public async Task HungUiaCallKeepsOnlyOneMtaWorkerAndQueueIsBounded()
    {
        using var scheduler = new UiaWorkScheduler();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;
        try
        {
            Task<int> first = scheduler.RunAsync(() =>
            {
                Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
                Interlocked.Increment(ref executions); started.SetResult(); release.Wait(); return 1;
            }, CancellationToken.None, 500);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => first);
            Task<int> second = scheduler.RunAsync(() => Interlocked.Increment(ref executions), CancellationToken.None, 100);
            Task<int> third = scheduler.RunAsync(() => Interlocked.Increment(ref executions), CancellationToken.None, 100);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.RunAsync(() => 4, CancellationToken.None));
            await Assert.ThrowsAsync<TimeoutException>(() => second);
            await Assert.ThrowsAsync<TimeoutException>(() => third);
            Assert.Equal(1, executions);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task CancelledQueuedResolutionCannotPublishLateTarget()
    {
        using var scheduler = new UiaWorkScheduler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel(); int calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scheduler.RunAsync(() => ++calls, cancellation.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void FreshnessRejectsGeometryDpiClosedAndUnrelatedSiblingButAllowsOwnedDialog()
    {
        var original = new TargetWindowContext(1, 5, 1, new Rect(-1000, 0, 1000, 800), 144);
        Assert.True(original.Matches(original with { }));
        Assert.False(original.Matches(original with { Bounds = new Rect(-900, 0, 1000, 800) }));
        Assert.False(original.Matches(original with { Dpi = 192 }));
        Assert.False(original.Matches(null));
        Assert.False(original.Owns(original with { Hwnd = 2, RootOwner = 2 }));
        Assert.True(original.Owns(original with { Hwnd = 3 }));
    }

    [Fact]
    public async Task AllSixPrimitivesActuallyRenderAndMotionCanBeReplacedAndStopped()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                foreach (AnnotationStyle style in Enum.GetValues<AnnotationStyle>())
                {
                    var canvas = new Canvas { Width = 500, Height = 300 };
                    var shape = BuddySurface.CreateAnnotation(style, new Rect(100, 80, 80, 30), new Point(230, 110), new Size(500, 300));
                    Assert.False(shape.IsHitTestVisible);
                    canvas.Children.Add(shape);
                    canvas.Measure(new Size(500, 300)); canvas.Arrange(new Rect(0, 0, 500, 300));
                    var bitmap = new RenderTargetBitmap(500, 300, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(canvas);
                    var pixels = new byte[500 * 300 * 4]; bitmap.CopyPixels(pixels, 2000, 0);
                    Assert.Contains(pixels, value => value > 0);
                }
                var transform = new TranslateTransform();
                BuddySurface.Move(transform, new Point(), new Point(300, 200));
                BuddySurface.Move(transform, new Point(300, 200), new Point(10, 10));
                BuddySurface.StopTransform(transform);
                Assert.False(transform.HasAnimatedProperties);
                double stoppedX = transform.X;
                Thread.Sleep(320);
                Assert.Equal(stoppedX, transform.X);
                done.SetResult();
            }
            catch (Exception ex) { done.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
