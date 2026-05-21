using System.Windows;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class App : WpfApplication
{
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureDirectories();

        var configStore = new ConfigStore();
        var profileManager = new ProfileManager(configStore);
        L10n.Initialize(profileManager.Config.Language);
        var chromeManager = new ChromeManager();
        var updateService = new UpdateService();
        var mainViewModel = new MainViewModel(profileManager, chromeManager, updateService);

        _mainWindow = new MainWindow(mainViewModel);
        MainWindow = _mainWindow;

        _trayService = new TrayService(_mainWindow, mainViewModel);
        _trayService.Initialize();

        _mainWindow.Show();
        mainViewModel.ShowDownloadIfNeeded();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
