using System.ComponentModel;
using System.Windows;
using ChromeIsolator.Services;

namespace ChromeIsolator;

public partial class DownloadWindow : Window
{
    private const string OfficialChromeDownloadUrl = "https://www.google.cn/chrome/";
    private readonly ChromeManager _chromeManager;
    private CancellationTokenSource? _cts;
    private string? _lastError;
    private bool _isInstalling;

    public bool DownloadSucceeded { get; private set; }
    public bool UseInstalled { get; private set; }
    public bool UseEdgeFallback { get; private set; }

    public DownloadWindow(ChromeManager chromeManager)
    {
        _chromeManager = chromeManager;
        InitializeComponent();
        IconHelper.ApplyIcon(this);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShowSetupState();
    }

    private void ShowSetupState()
    {
        _cts = null;
        _isInstalling = false;
        ProgressBar.Value = 0;
        PercentText.Text = "0%";
        RetryButton.Visibility = Visibility.Collapsed;
        CopyErrorButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Visible;
        OpenOfficialButton.Visibility = Visibility.Visible;
        UseInstalledButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        EdgeFallbackPanel.Visibility = _chromeManager.InstalledEdge is null ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Content = L10n.GetString("BtnClose");

        if (_chromeManager.InstalledChrome is not null)
        {
            TitleText.Text = L10n.GetString("BrowserSetupReadyTitle");
            NoticeText.Text = L10n.GetString("BrowserSetupChromeFoundNotice");
            StatusText.Text = L10n.GetString("ChromeReady");
            ProgressBar.Visibility = Visibility.Collapsed;
            PercentText.Visibility = Visibility.Collapsed;
            InstallButton.Visibility = Visibility.Collapsed;
            OpenOfficialButton.Visibility = Visibility.Collapsed;
            EdgeFallbackPanel.Visibility = Visibility.Collapsed;
            UseInstalledButton.Content = L10n.GetString("BtnStartUsing");
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ProgressBar.Visibility = Visibility.Visible;
            PercentText.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            TitleText.Text = L10n.GetString("DownloadPreparing");
            NoticeText.Text = L10n.GetString("BrowserSetupNotice");
            StatusText.Text = _chromeManager.InstalledEdge is null
                ? L10n.GetString("BrowserSetupChromeMissingNoEdge")
                : L10n.GetString("BrowserSetupChromeMissingWithEdge");
            UseInstalledButton.Content = L10n.GetString("BtnRefreshDetection");
        }
    }

    private async Task StartDownloadAsync()
    {
        _lastError = null;
        RetryButton.Visibility = Visibility.Collapsed;
        CopyErrorButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Collapsed;
        OpenOfficialButton.Visibility = Visibility.Collapsed;
        EdgeFallbackPanel.Visibility = Visibility.Collapsed;
        UseInstalledButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        CancelButton.Content = L10n.GetString("BtnCancel");
        ProgressBar.Value = 0;
        PercentText.Text = "0%";
        StatusText.Text = L10n.GetString("DownloadPreparingStatus");

        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                ProgressBar.Value = report.Percent;
                PercentText.Text = $"{report.Percent:F0}%";
                StatusText.Text = report.Status;
                if (report.Percent >= 80)
                {
                    _isInstalling = true;
                    CancelButton.IsEnabled = false;
                }
            });

            await _chromeManager.PrepareChromeAsync(progress, _cts.Token);
            _isInstalling = false;
            DownloadSucceeded = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            _isInstalling = false;
            StatusText.Text = L10n.GetString("BtnCancel");
        }
        catch (Exception ex)
        {
            _isInstalling = false;
            _lastError = ex.ToString();
            StatusText.Text = $"{L10n.GetString("ChromeInstallFailed")}: {ex.Message}";
            CancelButton.Content = L10n.GetString("BtnClose");
            CancelButton.IsEnabled = true;
            RetryButton.Visibility = Visibility.Visible;
            CopyErrorButton.Visibility = Visibility.Visible;
            OpenOfficialButton.Visibility = Visibility.Visible;
            EdgeFallbackPanel.Visibility = _chromeManager.InstalledEdge is null ? Visibility.Collapsed : Visibility.Visible;
            UseInstalledButton.IsEnabled = true;
        }
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        await StartDownloadAsync();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            L10n.GetString("MsgConfirmInstallChrome"),
            L10n.GetString("AppTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await StartDownloadAsync();
        }
    }

    private void CopyErrorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastError is not null)
        {
            System.Windows.Clipboard.SetText(_lastError);
        }
    }

    private void OpenOfficialButton_Click(object sender, RoutedEventArgs e)
    {
        ShellService.OpenUrl(OfficialChromeDownloadUrl);
    }

    private void UseInstalledButton_Click(object sender, RoutedEventArgs e)
    {
        if (_chromeManager.InstalledChrome is not null)
        {
            UseInstalled = true;
            DialogResult = true;
            return;
        }

        ShowSetupState();
    }

    private void UseEdgeButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            L10n.GetString("MsgConfirmUseEdgeFallback"),
            L10n.GetString("AppTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            UseEdgeFallback = true;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            CancelButton.Content = L10n.GetString("BtnClose");
        }
        else
        {
            DialogResult = false;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isInstalling)
        {
            e.Cancel = true;
            return;
        }

        _cts?.Cancel();
        base.OnClosing(e);
    }
}
