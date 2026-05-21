using ChromeIsolator.Services;

namespace ChromeIsolator.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ChromeManager _chromeManager;
    private readonly UpdateService _updateService;
    private string _chromeStatusText = "";
    private string _updateStatusText = "";
    private bool _isCheckingForUpdates;

    public SettingsViewModel(ChromeManager chromeManager, UpdateService updateService)
    {
        _chromeManager = chromeManager;
        _updateService = updateService;

        OpenDataFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.SupportDir));
        OpenProfilesFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.ProfilesDir));
        OpenChromeFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.ChromeDir));
        CopyDataPathCommand = new RelayCommand(() => ShellService.CopyText(AppPaths.SupportDir));
        CheckUpdatesCommand = new RelayCommand(CheckForUpdates, () => !IsCheckingForUpdates);
        OpenReleasesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.ReleasesUrl));
        OpenIssuesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.IssuesUrl));
        CopyEmailCommand = new RelayCommand(() => ShellService.CopyText("lucas6.zju@vip.163.com"));

        RefreshChromeStatus();
        UpdateStatusText = $"当前版本：{_updateService.CurrentVersion}";
    }

    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenProfilesFolderCommand { get; }
    public RelayCommand OpenChromeFolderCommand { get; }
    public RelayCommand CopyDataPathCommand { get; }
    public RelayCommand CheckUpdatesCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public RelayCommand OpenIssuesCommand { get; }
    public RelayCommand CopyEmailCommand { get; }

    public string DataPath => AppPaths.SupportDir;
    public string ProfilesPath => AppPaths.ProfilesDir;
    public string ChromePath => AppPaths.ChromeDir;
    public string CurrentVersion => _updateService.CurrentVersion;

    public string ChromeStatusText
    {
        get => _chromeStatusText;
        private set => SetProperty(ref _chromeStatusText, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdates, value))
            {
                CheckUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async void CheckForUpdates()
    {
        try
        {
            IsCheckingForUpdates = true;
            UpdateStatusText = "正在检查更新...";
            var result = await _updateService.CheckForUpdatesAsync();
            UpdateStatusText = result.Status switch
            {
                UpdateCheckStatus.UpToDate => $"当前已是最新版本（{result.LatestVersion ?? CurrentVersion}）",
                UpdateCheckStatus.UpdateAvailable => $"发现新版本：{result.LatestVersion}",
                _ => $"检查更新失败：{result.ErrorMessage ?? "未知错误"}"
            };
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private void RefreshChromeStatus()
    {
        var chrome = _chromeManager.CurrentChrome;
        ChromeStatusText = chrome is null
            ? "Chrome 未找到"
            : $"Chrome 可用：{chrome.Version ?? "未知版本"}（{chrome.Source}）\n{chrome.ExecutablePath}";
    }
}
