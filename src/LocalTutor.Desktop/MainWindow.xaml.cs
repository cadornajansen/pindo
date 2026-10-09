using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using LocalTutor.Core;
using LocalTutor.Desktop.Interop;
using LocalTutor.Desktop.Services;

namespace LocalTutor.Desktop;

public partial class MainWindow : Window
{
    private readonly ILocalTutorService _tutorService;
    private readonly IOllamaClient _ollamaClient;
    private readonly CancellationTokenSource _lifetime = new();
    private GlobalHotkey? _hotkey;
    private nint _previousForegroundWindow;
    private bool _isSubmitting;

    public MainWindow(ILocalTutorService tutorService, IOllamaClient ollamaClient, nint foregroundWindow)
    {
        _tutorService = tutorService;
        _ollamaClient = ollamaClient;
        _previousForegroundWindow = foregroundWindow;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)
                            ?? throw new InvalidOperationException("The assistant window is not ready.");
        _hotkey = new GlobalHotkey(source);
        _hotkey.Pressed += ShowFromHotkey;
        HideButton.IsEnabled = _hotkey.IsRegistered;
        HotkeyStatus.Text = _hotkey.IsRegistered
            ? "Ctrl + Space to open  ·  Esc to hide  ·  × to exit"
            : $"Ctrl + Space unavailable (Windows error {_hotkey.RegistrationError}). " +
              "Another app may be using it. Keep this window open or use the taskbar.";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusInput();
        try
        {
            bool available = await _ollamaClient.IsAvailableAsync(_lifetime.Token);
            OllamaStatus.Text = available
                ? "Ollama detected locally · Tutor still uses mock responses"
                : "Ollama unavailable · Mock tutor is ready";
        }
        catch (OperationCanceledException)
        {
            // Closing the window cancels the optional availability check.
        }
    }

    private void ShowFromHotkey(nint foregroundWindow)
    {
        nint ownWindow = new WindowInteropHelper(this).Handle;
        if (foregroundWindow != nint.Zero && foregroundWindow != ownWindow)
        {
            _previousForegroundWindow = foregroundWindow;
        }

        Show();
        WindowState = WindowState.Normal;
        NativeMethods.SetForegroundWindow(ownWindow);
        Activate();
        FocusInput();
    }

    private void FocusInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (IsVisible)
            {
                InstructionInput.Focus();
                Keyboard.Focus(InstructionInput);
            }
        }));
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        string instruction = InstructionInput.Text.Trim();
        if (_isSubmitting)
        {
            return;
        }

        if (instruction.Length == 0)
        {
            ResponseText.Text = "Type a question first. English, Filipino, or Taglish is welcome.";
            FocusInput();
            return;
        }

        _isSubmitting = true;
        SubmitButton.IsEnabled = false;
        try
        {
            TutorRequest request = new(instruction, GetActiveApplicationName(), Array.Empty<UiElementSnapshot>());
            TutorResponse response = await _tutorService.GetNextStepAsync(request, _lifetime.Token);
            ResponseText.Text = response.Success
                ? response.Instruction
                : response.Error ?? "The tutor could not prepare a step. Please try again.";
        }
        catch (OperationCanceledException)
        {
            ResponseText.Text = "Request cancelled.";
        }
        catch (Exception)
        {
            ResponseText.Text = "The tutor could not respond. Please try again.";
        }
        finally
        {
            _isSubmitting = false;
            SubmitButton.IsEnabled = true;
            FocusInput();
        }
    }

    private string GetActiveApplicationName()
    {
        NativeMethods.GetWindowThreadProcessId(_previousForegroundWindow, out uint processId);
        if (processId == 0 || processId > int.MaxValue)
        {
            return "Unknown application";
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "Unknown application";
        }
        catch (InvalidOperationException)
        {
            return "Unknown application";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Unknown application";
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideIfRecoverable();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _hotkey?.IsRegistered == true)
        {
            HideIfRecoverable();
            e.Handled = true;
        }
    }

    private void HideIfRecoverable()
    {
        if (_hotkey?.IsRegistered == true)
        {
            Hide();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is System.Windows.Controls.TextBlock)
        {
            DragMove();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnClosed(e);
    }
}
