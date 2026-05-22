using System.Windows;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = @"Local\ChromeIsolator.SingleInstance";
    private const string SingleInstancePipeName = "ChromeIsolator.SingleInstance";
    private const int ASFW_ANY = -1;

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstance;
    private CancellationTokenSource? _singleInstanceCts;
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
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
            if (!isFirstInstance)
            {
                NotifyExistingInstance();
                Shutdown();
                return;
            }
            _ownsSingleInstance = true;

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
            StartSingleInstanceListener();
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

        _singleInstanceCts?.Cancel();
        _singleInstanceCts?.Dispose();
        _trayService?.Dispose();
        if (_ownsSingleInstance)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();
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

    private void StartSingleInstanceListener()
    {
        _singleInstanceCts = new CancellationTokenSource();
        var token = _singleInstanceCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        SingleInstancePipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    _ = await reader.ReadLineAsync(token).ConfigureAwait(false);

                    await Dispatcher.InvokeAsync(() => _mainWindow?.ShowFromTray()).Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Keep listening; a failed wake-up request should not break the app.
                }
            }
        }, token);
    }

    private static void NotifyExistingInstance()
    {
        AllowSetForegroundWindow(ASFW_ANY);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
                client.Connect(250);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine("show");
                return;
            }
            catch
            {
                Thread.Sleep(150);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
