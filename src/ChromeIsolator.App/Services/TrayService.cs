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
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
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

    private void NotifyIcon_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || _notifyIcon is null)
        {
            return;
        }

        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.ContextMenuStrip.Show(Cursor.Position);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Clear();

        menu.Items.Add(L10n.GetString("TrayOpenPanel"), null, (_, _) => _mainWindow.ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());

        var running = _viewModel.Profiles.Where(p => p.IsRunning).ToList();
        if (running.Count > 0)
        {
            foreach (var profile in running)
            {
                menu.Items.Add(L10n.Format("TrayStopProfile", profile.Title), null, (_, _) => _ = _viewModel.StopProfileSafeAsync(profile));
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L10n.GetString("TrayStopAll"), null, (_, _) => _ = _viewModel.StopAllSafeAsync());
            menu.Items.Add(new ToolStripSeparator());
        }

        var stopped = _viewModel.Profiles
            .Where(p => !p.IsRunning && !p.IsStarting && !p.IsStopping)
            .OrderByDescending(p => p.LastUsed ?? DateTime.MinValue)
            .ToList();
        foreach (var profile in stopped)
        {
            menu.Items.Add(L10n.Format("TrayStartProfile", profile.Title), null, (_, _) => _viewModel.StartProfile(profile));
        }

        if (stopped.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(L10n.GetString("TrayCheckUpdates"), null, async (_, _) => await _viewModel.CheckForUpdatesFromTrayAsync());
        menu.Items.Add(new ToolStripSeparator());
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
        });

        return menu;
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(path))
        {
            return new Icon(path);
        }

        var assembly = typeof(TrayService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                return new Icon(stream);
            }
        }

        return SystemIcons.Application;
    }
}
