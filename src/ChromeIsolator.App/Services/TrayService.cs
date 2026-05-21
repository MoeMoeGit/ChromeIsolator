using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ChromeIsolator.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;

namespace ChromeIsolator.Services;

public sealed class TrayService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _viewModel;
    private NotifyIcon? _notifyIcon;

    public TrayService(MainWindow mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = L10n.GetString("AppTitle"),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _mainWindow.ShowFromTray();
        _notifyIcon.ContextMenuStrip = BuildMenu();
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            menu.Items.Add(L10n.GetString("TrayOpenPanel"), null, (_, _) => _mainWindow.ShowFromTray());
            menu.Items.Add(new ToolStripSeparator());

            foreach (var profile in _viewModel.Profiles)
            {
                var text = profile.IsRunning
                    ? L10n.Format("TrayStopProfile", profile.Title)
                    : L10n.Format("TrayStartProfile", profile.Title);
                menu.Items.Add(text, null, (_, _) => _viewModel.ToggleProfile(profile));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L10n.GetString("TrayStopAll"), null, (_, _) => _viewModel.StopAll());
            menu.Items.Add(L10n.GetString("TrayStopAllAndQuit"), null, async (_, _) =>
            {
                var runningCount = _viewModel.Profiles.Count(p => p.IsRunning);
                if (runningCount > 0)
                {
                    var result = MessageBox.Show(
                        L10n.Format("MsgRunningEnvExit", runningCount),
                        L10n.GetString("AppTitle"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                _mainWindow.ExitFromTray();
                await _viewModel.StopAllAndQuitAsync();
            });
            menu.Items.Add(L10n.GetString("TrayCheckUpdates"), null, async (_, _) => await _viewModel.CheckForUpdatesFromTrayAsync());
            menu.Items.Add(L10n.GetString("TrayExit"), null, (_, _) =>
            {
                var runningCount = _viewModel.Profiles.Count(p => p.IsRunning);
                if (runningCount > 0)
                {
                    var result = MessageBox.Show(
                        L10n.Format("MsgRunningEnvExit", runningCount),
                        L10n.GetString("AppTitle"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                _mainWindow.ExitFromTray();
                Application.Current.Shutdown();
            });
        };

        return menu;
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }
}
