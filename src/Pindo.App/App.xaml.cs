using System.Windows;
using Microsoft.Win32;
namespace Pindo.App;
public partial class App : Application
{
    private Mutex? _singleInstance;
    private UtilityController? _controller;
    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "Local\\Pindo.Desktop.Instance", out bool first);
        if (!first)
        {
            MessageBox.Show("Pindo is already running. Use its tray icon or Ctrl+Windows.", "Pindo");
            _singleInstance.Dispose(); _singleInstance = null; Shutdown(); return;
        }
        base.OnStartup(e);
        try
        {
            _controller = new UtilityController();
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerChanged;
            SessionEnding += OnSessionEnding;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Pindo could not start: " + ex.Message, "Pindo");
            Shutdown(1);
        }
    }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) StopSession();
    }
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) StopSession();
    }
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e) => StopSession();
    private void StopSession() => Dispatcher.Invoke(() => _controller?.Suspend());
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SessionEnding -= OnSessionEnding;
        _controller?.Dispose();
        if (_singleInstance is not null) { _singleInstance.ReleaseMutex(); _singleInstance.Dispose(); }
        base.OnExit(e);
    }
}
