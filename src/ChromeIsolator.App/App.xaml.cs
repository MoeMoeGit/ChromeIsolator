using System.Windows;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = @"Local\ChromeIsolator.SingleInstance";
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
            var externalUrl = ExtractExternalUrl(e.Args);
            if (!isFirstInstance)
            {
                if (!NotifyExistingInstance(externalUrl))
                {
                    ShowSingleInstanceNotifyFailed(externalUrl);
                }
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
            _mainWindow.ApplySavedPlacement();

            _trayService = new TrayService(_mainWindow, mainViewModel);
            _trayService.Initialize();

            StartSingleInstanceListener(mainViewModel);
            if (externalUrl is not null)
            {
                mainViewModel.HandleExternalLink(externalUrl);
            }
            else
            {
                _mainWindow.Show();
                mainViewModel.ShowDownloadIfNeeded();
            }
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
            if (_chromeManager?.HasRunningProfiles == true)
            {
                _chromeManager.StopAllAsync(_profileManager?.Config.Profiles ?? []).Wait(TimeSpan.FromSeconds(5));
            }
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

    private void StartSingleInstanceListener(MainViewModel mainViewModel)
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
                    var message = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (TryParseExternalUrlMessage(message, out var url))
                    {
                        await Dispatcher.InvokeAsync(() => mainViewModel.HandleExternalLink(url)).Task.ConfigureAwait(false);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => _mainWindow?.ShowFromTray()).Task.ConfigureAwait(false);
                    }
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

    private static bool NotifyExistingInstance(string? externalUrl)
    {
        AllowSetForegroundWindow(ASFW_ANY);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
                client.Connect(500);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(CreateSingleInstanceMessage(externalUrl));
                return true;
            }
            catch
            {
                Thread.Sleep(250);
            }
        }

        return false;
    }

    private static void ShowSingleInstanceNotifyFailed(string? externalUrl)
    {
        try
        {
            string message;
            if (string.IsNullOrWhiteSpace(externalUrl))
            {
                message = "ChromeIsolator is already running, but this instance could not wake it. Open ChromeIsolator from the system tray, or exit the existing process and try again.";
            }
            else
            {
                TryCopyText(externalUrl);
                message = $"ChromeIsolator is already running, but this instance could not send the link to it. The link has been copied.\n\n{externalUrl}";
            }

            System.Windows.MessageBox.Show(
                message,
                "ChromeIsolator",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // This process is only a wake-up forwarder; nothing else can be done here.
        }
    }

    private static void TryCopyText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard access can fail if another process owns it.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private static string? ExtractExternalUrl(IEnumerable<string> args)
    {
        return args.FirstOrDefault(arg =>
            Uri.TryCreate(arg, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https");
    }

    private static string CreateSingleInstanceMessage(string? externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalUrl))
        {
            return "show";
        }

        return "open-url " + Convert.ToBase64String(Encoding.UTF8.GetBytes(externalUrl));
    }

    private static string SingleInstancePipeName
    {
        get
        {
            try
            {
                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    return "ChromeIsolator.SingleInstance." + Convert.ToHexString(Encoding.UTF8.GetBytes(sid));
                }
            }
            catch
            {
                // Fall back to the historical name if Windows identity lookup fails.
            }

            return "ChromeIsolator.SingleInstance";
        }
    }

    private static bool TryParseExternalUrlMessage(string? message, out string url)
    {
        url = "";
        const string prefix = "open-url ";
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            url = Encoding.UTF8.GetString(Convert.FromBase64String(message[prefix.Length..]));
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
        }
        catch
        {
            url = "";
            return false;
        }
    }
}
