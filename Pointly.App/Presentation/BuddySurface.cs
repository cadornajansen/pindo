using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Pointly.App.Interop;

namespace Pointly.App.Presentation;

internal sealed class BuddySurface : Window
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly Canvas _annotations = new() { IsHitTestVisible = false };
    private readonly Border _bubble;
    private readonly TextBlock _instruction = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Brushes.White };
    private readonly TextBlock _state = new() { FontSize = 10, Foreground = Brushes.Aquamarine, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Grid _buddy = new() { Width = 48, Height = 48 };
    private readonly TranslateTransform _motion = new();
    private readonly TranslateTransform _bubbleMotion = new();
    private Point? _position;
    public MonitorGeometry Monitor { get; }
    public double Scale => NativeMethods.GetDpiForWindow(new WindowInteropHelper(this).Handle) / 96d;

    public BuddySurface(MonitorGeometry monitor)
    {
        Monitor = monitor;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 1; Height = 1;
        var body = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 8,3 Q 3,3 3,10 L 3,34 Q 3,42 11,42 L 24,42 L 34,47 L 32,37 L 42,17 Q 47,7 36,5 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x75, 0xE6, 0xCB)), Stroke = new SolidColorBrush(Color.FromRgb(0x15,0x25,0x36)), StrokeThickness = 2
        };
        _buddy.Children.Add(body);
        var face = new Canvas();
        foreach (double x in new[] { 13d, 27d })
        {
            var eye = new Ellipse { Width = 4, Height = 6, Fill = Brushes.MidnightBlue };
            Canvas.SetLeft(eye, x); Canvas.SetTop(eye, 16); face.Children.Add(eye);
        }
        var smile = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 15,28 Q 21,33 27,28"), Stroke = Brushes.MidnightBlue, StrokeThickness = 2 };
        face.Children.Add(smile); _buddy.Children.Add(face);
        _buddy.RenderTransform = _motion;
        var content = new StackPanel(); content.Children.Add(_instruction); content.Children.Add(_state);
        _bubble = new Border { Background = new SolidColorBrush(Color.FromRgb(0x18,0x24,0x35)), BorderBrush = Brushes.Aquamarine,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Width = 264, MaxHeight = 108,
            Child = content, RenderTransform = _bubbleMotion };
        _canvas.Children.Add(_annotations); _canvas.Children.Add(_buddy); _canvas.Children.Add(_bubble);
        Content = _canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint hwnd = new WindowInteropHelper(this).Handle;
        long styles = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (nint)(styles | NativeMethods.WS_EX_TRANSPARENT |
            NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW));
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        HwndSource.FromHwnd(hwnd)?.AddHook((nint _, int message, nint w, nint l, ref bool handled) =>
        {
            if (message == 0x21) { handled = true; return (nint)3; } // MA_NOACTIVATE
            if (message == 0x84) { handled = true; return (nint)(-1); } // HTTRANSPARENT
            return 0;
        });
        DesktopGeometry.SetWindowPos(hwnd, (nint)(-1), (int)Monitor.Bounds.X, (int)Monitor.Bounds.Y,
            (int)Monitor.Bounds.Width, (int)Monitor.Bounds.Height, 0x0010 | 0x0040);
    }

    public void Render(GuidancePresentation presentation, Point anchor, string state)
    {
        if (!IsVisible) Show();
        // WPF's initial Show can apply its logical Width/Height after SourceInitialized.
        // Reapply the monitor's physical bounds at the final native rendering boundary.
        DesktopGeometry.SetWindowPos(new WindowInteropHelper(this).Handle, (nint)(-1),
            (int)Monitor.Bounds.X, (int)Monitor.Bounds.Y, (int)Monitor.Bounds.Width, (int)Monitor.Bounds.Height, 0x0010);
        double scale = Scale;
        Rect target = presentation.Target ?? new Rect(anchor.X, anchor.Y, 1, 1);
        BuddyPlacement placement = PresentationGeometry.Place(Monitor.WorkArea, target, scale);
        Point next = PresentationGeometry.ToDip(placement.Buddy.TopLeft, Monitor.Bounds, scale);
        Point bubble = PresentationGeometry.ToDip(placement.Bubble.TopLeft, Monitor.Bounds, scale);
        _instruction.Text = presentation.Instruction.Length > 360 ? presentation.Instruction[..357] + "…" : presentation.Instruction;
        _state.Text = state;
        _bubble.Width = placement.Bubble.Width / scale;
        Move(_motion, _position ?? next, next);
        Move(_bubbleMotion, new Point(_bubbleMotion.X, _bubbleMotion.Y), bubble, _position is not null);
        _position = next;
        _annotations.Children.Clear();
        if (presentation.Target is { } physical)
        {
            Point top = PresentationGeometry.ToDip(physical.TopLeft, Monitor.Bounds, scale);
            Rect dip = new(top, new Size(physical.Width / scale, physical.Height / scale));
            _annotations.Children.Add(CreateAnnotation(presentation.Style, dip, new Point(next.X + 24, next.Y + 24),
                new Size(Monitor.Bounds.Width / scale, Monitor.Bounds.Height / scale)));
        }
    }

    public void SetState(string state) => _state.Text = state;
    public void SetPartial(string partial) => _state.Text = "Listening: " + (partial.Length > 90 ? partial[..87] + "…" : partial);
    public void ClearTarget() => _annotations.Children.Clear();
    public void StopMotion()
    {
        foreach (TranslateTransform transform in new[] { _motion, _bubbleMotion })
        {
            StopTransform(transform);
        }
    }

    internal static void StopTransform(TranslateTransform transform)
    {
        double x = transform.X, y = transform.Y;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.X = x; transform.Y = y;
    }

    internal static void Move(TranslateTransform transform, Point from, Point to, bool animate = true)
    {
        from = new Point(transform.HasAnimatedProperties ? transform.X : from.X, transform.HasAnimatedProperties ? transform.Y : from.Y);
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.X = to.X; transform.Y = to.Y;
        if (!animate || !SystemParameters.ClientAreaAnimation) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from.X, to.X, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from.Y, to.Y, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
    }

    internal static Shape CreateAnnotation(AnnotationStyle style, Rect target, Point buddy, Size surface)
    {
        Brush ink = new SolidColorBrush(Color.FromRgb(0x13, 0xBD, 0xA0));
        Shape shape;
        Point center = new(target.X + target.Width / 2, target.Y + target.Height / 2);
        switch (style)
        {
            case AnnotationStyle.Pointer:
                Point tip = new(Math.Clamp(buddy.X, target.Left, target.Right), Math.Clamp(buddy.Y, target.Top, target.Bottom));
                Vector direction = tip - buddy;
                if (direction.Length < 1) direction = new Vector(0, 1);
                direction.Normalize(); Vector side = new(-direction.Y, direction.X);
                var geometry = new StreamGeometry();
                using (StreamGeometryContext pen = geometry.Open())
                {
                    pen.BeginFigure(buddy + direction * 29, false, false); pen.LineTo(tip, true, false);
                    pen.BeginFigure(tip - direction * 10 + side * 5, false, false); pen.LineTo(tip, true, false); pen.LineTo(tip - direction * 10 - side * 5, true, false);
                }
                shape = new System.Windows.Shapes.Path { Data = geometry };
                break;
            case AnnotationStyle.Ring:
                shape = new Ellipse { Width = Math.Max(28, target.Width), Height = Math.Max(28, target.Height) };
                Canvas.SetLeft(shape, center.X - shape.Width / 2); Canvas.SetTop(shape, center.Y - shape.Height / 2);
                break;
            case AnnotationStyle.Underline:
            case AnnotationStyle.Stroke:
                shape = new Polyline { Points = style == AnnotationStyle.Underline
                    ? new PointCollection { new(target.Left, target.Bottom + 4), new(target.Right, target.Bottom + 4) }
                    : new PointCollection { new(target.Left, target.Bottom + 5), new(center.X, target.Bottom + 8), new(target.Right, target.Bottom + 5) } };
                break;
            case AnnotationStyle.Spotlight:
                var outer = new RectangleGeometry(new Rect(new Point(), surface));
                shape = new System.Windows.Shapes.Path { Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, new RectangleGeometry(target)), Fill = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)) };
                break;
            default:
                shape = new Rectangle { Width = target.Width, Height = target.Height, RadiusX = 4, RadiusY = 4 };
                Canvas.SetLeft(shape, target.Left); Canvas.SetTop(shape, target.Top);
                break;
        }
        shape.Stroke = ink; shape.StrokeThickness = 3; shape.IsHitTestVisible = false;
        return shape;
    }
}
