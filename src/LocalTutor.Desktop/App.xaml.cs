using System.Net.Http;
using System.Windows;
using LocalTutor.Desktop.Interop;
using LocalTutor.Desktop.Services;

namespace LocalTutor.Desktop;

public partial class App : Application
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    protected override void OnStartup(StartupEventArgs e)
    {
        // Capture the other app before our own window takes focus.
        nint foregroundWindow = NativeMethods.GetForegroundWindow();
        base.OnStartup(e);
        OllamaSettings settings = new();
        MainWindow = new MainWindow(new MockTutorService(),
            new OllamaAvailabilityClient(_httpClient, settings), foregroundWindow);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient.Dispose();
        base.OnExit(e);
    }
}
