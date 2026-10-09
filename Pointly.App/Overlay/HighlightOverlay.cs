using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Pointly.App.Interop;

namespace Pointly.App.Overlay;

/// <summary>
/// Transparent, topmost, click-through window spanning the virtual screen.
/// Renders a highlight frame around one arbitrary screen-space rectangle.
/// Never takes focus and never intercepts mouse input.
/// </summary>
public sealed class HighlightOverlay : Window
{
    private readonly Canvas _canvas;
    private readonly Rectangle _frame;
    private readonly Ellipse _ring;

    public HighlightOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // SystemParameters are already in device-independent pixels.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        IsHitTestVisible = false;

        _frame = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(0x26, 0xE5, 0x39, 0x35)),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };

        _canvas = new Canvas { IsHitTestVisible = false };
        _canvas.Children.Add(_frame);
        _ring = new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(0x26, 0xE5, 0x39, 0x35)),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(_ring);
        Content = _canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Belt and suspenders: WPF-level flags above plus extended window styles
        // so the overlay is truly click-through at the Win32 level.
        nint hwnd = new WindowInteropHelper(this).Handle;
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_NOACTIVATE
            | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        // Best effort: keep Pointly's own overlay out of screen captures.
        // Window capture (CreateForWindow) already excludes other windows;
        // this additionally hides the overlay from monitor-capture tools.
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    /// <summary>
    /// Shows the highlight at <paramref name="physicalBounds"/> (physical screen
    /// pixels, as reported by UI Automation). Converts to DIPs for rendering,
    /// so the frame aligns under per-monitor DPI scaling.
    /// </summary>
    public void ShowHighlight(Rect physicalBounds)
    {
        if (physicalBounds.IsEmpty || physicalBounds.Width <= 0 || physicalBounds.Height <= 0)
        {
            HideHighlight();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        // UIA reports physical pixels; WPF renders in DIPs.
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        Rect dip = physicalBounds;
        dip.Transform(fromDevice);
        dip.Inflate(3, 3);

        Canvas.SetLeft(_frame, dip.X - Left);
        Canvas.SetTop(_frame, dip.Y - Top);
        _frame.Width = dip.Width;
        _frame.Height = dip.Height;
        _frame.Visibility = Visibility.Visible;
        _ring.Visibility = Visibility.Hidden;
    }

    public void ShowPointHighlight(Rect physicalBounds)
    {
        if (physicalBounds.IsEmpty || physicalBounds.Width <= 0 || physicalBounds.Height <= 0)
        {
            HideHighlight();
            return;
        }

        if (!IsVisible) Show();
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        Rect dip = physicalBounds;
        dip.Transform(fromDevice);
        Canvas.SetLeft(_ring, dip.X - Left);
        Canvas.SetTop(_ring, dip.Y - Top);
        _ring.Width = dip.Width;
        _ring.Height = dip.Height;
        _ring.Visibility = Visibility.Visible;
        _frame.Visibility = Visibility.Hidden;
    }

    public void HideHighlight()
    {
        _frame.Visibility = Visibility.Hidden;
        _ring.Visibility = Visibility.Hidden;
    }
}
