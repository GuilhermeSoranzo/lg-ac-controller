using System.Windows;
using LGClimaTray.Services;
using Forms = System.Windows.Forms;

namespace LGClimaTray;

public sealed class TrayController : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _icon;

    public TrayController(MainWindow window)
    {
        _window = window;
        _icon = new Forms.NotifyIcon
        {
            Text = TrayIcon.Tooltip(window.Climate.State),
            Icon = TrayIcon.Create(window.Climate.State),
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        window.Climate.StateChanged += state => _window.Dispatcher.Invoke(() => UpdateIcon(state));
        // Left click opens/hides the panel; right click shows the menu only
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) TogglePanel(); };
        _icon.ContextMenuStrip.Items.Add("Abrir controle", null, (_, _) => ShowPanel());
        var startup = new Forms.ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = StartupRegistration.IsEnabled };
        startup.CheckedChanged += (_, _) => StartupRegistration.SetEnabled(startup.Checked);
        _icon.ContextMenuStrip.Items.Add(startup);
        _icon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _icon.ContextMenuStrip.Items.Add("Fechar", null, (_, _) => Exit());
    }

    private void UpdateIcon(AirConditionerState state)
    {
        var old = _icon.Icon;
        _icon.Icon = TrayIcon.Create(state);
        _icon.Text = TrayIcon.Tooltip(state);
        old?.Dispose();
    }

    public void TogglePanel()
    {
        if (_window.IsVisible) _window.Hide(); else ShowPanel();
    }

    public void ShowPanel()
    {
        _window.Show();
        _window.Activate();
        var work = SystemParameters.WorkArea;
        _window.Left = work.Right - _window.Width - 16;
        _window.Top = work.Bottom - _window.Height - 16;
    }

    private void Exit()
    {
        Dispose();
        _window.AllowClose = true;
        _window.Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
