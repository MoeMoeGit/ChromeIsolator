using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ChromeIsolator.ViewModels;
using Application = System.Windows.Application;

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
            Text = "浏览器多开",
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
            menu.Items.Add("打开管理面板", null, (_, _) => _mainWindow.ShowFromTray());
            menu.Items.Add(new ToolStripSeparator());

            foreach (var profile in _viewModel.Profiles)
            {
                var text = profile.IsRunning ? $"关闭 {profile.Title}" : $"启动 {profile.Title}";
                menu.Items.Add(text, null, (_, _) => _viewModel.ToggleProfile(profile));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("全部关闭", null, (_, _) => _viewModel.StopAll());
            menu.Items.Add("退出", null, (_, _) =>
            {
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
