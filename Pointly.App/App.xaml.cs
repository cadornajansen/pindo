using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Pointly.App.Automation;

namespace Pointly.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "Local\\Pindo.Desktop.Instance", out bool first);
        if (!first)
        {
            MessageBox.Show("Pindo is already running. Use its tray icon or Ctrl+Space.", "Pindo");
            _singleInstance.Dispose(); _singleInstance = null; Shutdown(); return;
        }
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        new WindowInteropHelper(window).EnsureHandle();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerChanged;
        SessionEnding += OnSessionEnding;
    }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) StopSession("WorkstationLocked");
    }
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) StopSession("SystemSuspending");
    }
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e) => StopSession("SessionEnding");
    private void StopSession(string reason) => Dispatcher.Invoke(() => (MainWindow as MainWindow)?.DismissSession(reason));
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SessionEnding -= OnSessionEnding;
        (MainWindow as MainWindow)?.DismissSession("ApplicationExit");
        UiaWorkScheduler.Shared.Dispose();
        if (_singleInstance is not null) { _singleInstance.ReleaseMutex(); _singleInstance.Dispose(); }
        base.OnExit(e);
    }
}
