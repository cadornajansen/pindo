using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Pointly.App.Presentation;

public sealed class BusyBorder : FrameworkElement
{
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private bool _busy;

    public BusyBorder()
    {
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => InvalidateVisual();
        IsVisibleChanged += (_, _) => Refresh();
        Unloaded += (_, _) => _timer.Stop();
    }

    public bool Busy
    {
        get => _busy;
        set { _busy = value; Refresh(); }
    }

    private void Refresh()
    {
        _timer.Stop();
        if (_busy && IsVisible && SystemParameters.ClientAreaAnimation)
        {
            _clock.Restart();
            _timer.Start();
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        if (!_busy || ActualWidth < 4 || ActualHeight < 4) return;
        var bounds = new Rect(1, 1, ActualWidth - 2, ActualHeight - 2);
        if (!SystemParameters.ClientAreaAnimation)
        {
            drawing.DrawRoundedRectangle(null, new Pen(Brushes.Aquamarine, 2), bounds, 16, 16);
            return;
        }
        PathGeometry path = new RectangleGeometry(bounds, 16, 16).GetFlattenedPathGeometry();
        double head = _clock.Elapsed.TotalMilliseconds / 1600 % 1;
        for (int index = 0; index < 24; index++)
        {
            double fraction = (head - 0.18 + index * 0.0075 + 1) % 1;
            path.GetPointAtFractionLength(fraction, out Point start, out _);
            path.GetPointAtFractionLength((fraction + 0.008) % 1, out Point end, out _);
            var ink = new SolidColorBrush(Color.FromArgb((byte)(30 + index * 9), 190, 255, 235));
            drawing.DrawLine(new Pen(ink, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, start, end);
        }
    }
}
