using System.Windows;

namespace LGClimaTray;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private MainWindow? _mainWindow;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one tray icon: a second launch just exits
        _singleInstance = new Mutex(true, @"Local\LGClimaTray", out var isFirstInstance);
        if (!isFirstInstance) { Shutdown(); return; }

        _mainWindow = new MainWindow();
        _tray = new TrayController(_mainWindow);

        _ = _mainWindow.ConnectAsync();

        // Started by Windows at login: stay in the tray without popping the panel
        if (!e.Args.Contains(StartupRegistration.TrayArgument)) _tray.ShowPanel();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
