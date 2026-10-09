using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Pointly.App.Interop;

namespace Pointly.App.Presentation;

public partial class ChatWindow : Window
{
    private MonitorGeometry? _monitor;
    private bool _exitRequested;
    private bool _busy;
    public event Action<string>? QuestionSubmitted;
    public event Action? MicrophoneRequested;
    public event Action? DismissRequested;
    public event Action? CheckRequested;

    public ChatWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PositionOnMonitor();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,
            NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    internal void Present(MonitorGeometry monitor, bool focusInput = false)
    {
        _monitor = monitor;
        double scale = NativeMethods.GetDpiForWindow(new WindowInteropHelper(this).EnsureHandle()) / 96d;
        Width = Math.Min(428, monitor.WorkArea.Width / scale - 24);
        bool entrance = !IsVisible;
        if (entrance) Show();
        PositionOnMonitor();
        if (entrance) AnimateEntrance();
        if (focusInput)
        {
            NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
            Activate();
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsVisible) return;
                Question.Focus();
                Keyboard.Focus(Question);
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void PositionOnMonitor()
    {
        if (_monitor is not { } monitor || !IsVisible) return;
        nint hwnd = new WindowInteropHelper(this).Handle;
        double scale = NativeMethods.GetDpiForWindow(hwnd) / 96d;
        int width = (int)Math.Round(ActualWidth * scale);
        int height = (int)Math.Round(ActualHeight * scale);
        int x = (int)Math.Round(monitor.WorkArea.Left + (monitor.WorkArea.Width - width) / 2);
        int y = (int)Math.Round(monitor.WorkArea.Bottom - height - 12 * scale);
        DesktopGeometry.SetWindowPos(hwnd, (nint)(-1), x, y, width, height, 0x0010);
    }

    private void AnimateEntrance()
    {
        Card.BeginAnimation(OpacityProperty, null);
        EntranceMotion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        Card.Opacity = 1;
        EntranceMotion.Y = 0;
        if (!SystemParameters.ClientAreaAnimation) return;
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
            { FillBehavior = FillBehavior.Stop });
        EntranceMotion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(18, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
    }

    internal void SetInstruction(string text)
    {
        Instruction.Text = text;
        Instruction.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    internal void SetState(string text) => State.Text = text;
    internal void SetBusy(bool busy)
    {
        _busy = busy;
        Question.IsEnabled = Send.IsEnabled = ClearButton.IsEnabled = MicrophoneButton.IsEnabled = !busy;
        CheckButton.IsEnabled = !busy;
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ThinkingBorder.Busy = busy;
    }
    internal void SetPartial(string text) => State.Text = "Listening: " + text;

    internal void SetMicrophoneState(bool isOn)
    {
        MicrophoneIcon.Text = isOn ? "\uE720" : "\uEC54";
        string action = isOn ? "Turn microphone off" : "Turn microphone on";
        MicrophoneButton.ToolTip = action;
        System.Windows.Automation.AutomationProperties.SetName(MicrophoneButton, action);
    }

    private void OnQuestionChanged(object sender, TextChangedEventArgs e)
    {
        if (Placeholder is null || Send is null) return;
        bool empty = string.IsNullOrWhiteSpace(Question.Text);
        Placeholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        Send.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; DismissRequested?.Invoke(); }
        else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            Submit();
        }
    }

    private void Submit()
    {
        if (_busy) return;
        string question = Question.Text.Trim();
        if (question.Length == 0) return;
        Question.Clear();
        QuestionSubmitted?.Invoke(question);
    }

    private void OnSend(object sender, RoutedEventArgs e) => Submit();
    private void OnClear(object sender, RoutedEventArgs e) { Question.Clear(); Question.Focus(); }
    private void OnMicrophone(object sender, RoutedEventArgs e) => MicrophoneRequested?.Invoke();
    private void OnCancel(object sender, RoutedEventArgs e) => DismissRequested?.Invoke();
    private void OnCheck(object sender, RoutedEventArgs e) => CheckRequested?.Invoke();
    internal void SetCanCheck(bool canCheck)
    {
        CheckButton.Visibility = canCheck ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Visibility = canCheck || _busy ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void Exit() { _exitRequested = true; Close(); }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested) { e.Cancel = true; DismissRequested?.Invoke(); }
        base.OnClosing(e);
    }
}
