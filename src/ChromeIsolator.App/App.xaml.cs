using System.Windows;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class App : WpfApplication
{
    private TrayService? _trayService;
    private MainWindow? _mainWindow;
    private ProfileManager? _profileManager;
    private ChromeManager? _chromeManager;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppPaths.EnsureDirectories();

            var configStore = new ConfigStore();
            _profileManager = new ProfileManager(configStore);
            L10n.Initialize(_profileManager.Config.Language);
            _chromeManager = new ChromeManager(() => _profileManager.Config.AllowEdgeFallback);
            var updateService = new UpdateService();
            var mainViewModel = new MainViewModel(_profileManager, _chromeManager, updateService);

            _mainWindow = new MainWindow(mainViewModel);
            MainWindow = _mainWindow;

            _trayService = new TrayService(_mainWindow, mainViewModel);
            _trayService.Initialize();

            _mainWindow.Show();
            mainViewModel.ShowDownloadIfNeeded();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _chromeManager?.StopAll(_profileManager?.Config.Profiles ?? []);
        }
        catch
        {
            // Process exit is already in progress; best-effort browser cleanup only.
        }

        _trayService?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowFatalError(e.Exception);
        Shutdown(1);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            System.Windows.MessageBox.Show(
                $"ChromeIsolator encountered a fatal error and cannot start.\n\n{ex.Message}\n\n{ex}",
                "ChromeIsolator",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If even MessageBox fails, write to stderr as last resort
            Console.Error.WriteLine($"[ChromeIsolator] Fatal error: {ex}");
        }
    }
}
