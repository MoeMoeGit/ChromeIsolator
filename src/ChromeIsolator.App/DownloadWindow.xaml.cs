using System.ComponentModel;
using System.Windows;
using ChromeIsolator.Services;

namespace ChromeIsolator;

public partial class DownloadWindow : Window
{
    private readonly ChromeManager _chromeManager;
    private CancellationTokenSource? _cts;

    public bool DownloadSucceeded { get; private set; }
    public bool UseInstalled { get; private set; }

    public DownloadWindow(ChromeManager chromeManager)
    {
        _chromeManager = chromeManager;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                ProgressBar.Value = report.Percent;
                PercentText.Text = $"{report.Percent:F0}%";
                StatusText.Text = report.Status;
            });

            await _chromeManager.PrepareChromeAsync(progress, _cts.Token);
            DownloadSucceeded = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = L10n.GetString("BtnCancel");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{L10n.GetString("ChromeInstallFailed")}: {ex.Message}";
            CancelButton.Content = L10n.GetString("BtnClose");
        }
    }

    private void UseInstalledButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        UseInstalled = true;
        DialogResult = false;
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
        _cts?.Cancel();
        base.OnClosing(e);
    }
}
