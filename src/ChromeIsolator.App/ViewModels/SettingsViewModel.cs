using ChromeIsolator.Services;

namespace ChromeIsolator.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ChromeManager _chromeManager;
    private readonly UpdateService _updateService;
    private readonly ProfileManager _profileManager;
    private readonly Action _reinstallChrome;
    private string _chromeStatusText = "";
    private string _updateStatusText = "";
    private bool _isCheckingForUpdates;
    private bool _showAdvancedDetails;
    private (string Code, string NativeName) _selectedLanguage;

    public SettingsViewModel(ChromeManager chromeManager, UpdateService updateService, ProfileManager profileManager, Action reinstallChrome)
    {
        _chromeManager = chromeManager;
        _updateService = updateService;
        _profileManager = profileManager;
        _reinstallChrome = reinstallChrome;
        _showAdvancedDetails = profileManager.Config.ShowAdvancedDetails;

        Languages = L10n.SupportedLanguages;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == L10n.CurrentLanguage);

        OpenDataFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.SupportDir));
        OpenProfilesFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.ProfilesDir));
        OpenChromeFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.ChromeDir));
        CopyDataPathCommand = new RelayCommand(() => ShellService.CopyText(AppPaths.SupportDir));
        CheckUpdatesCommand = new RelayCommand(CheckForUpdates, () => !IsCheckingForUpdates);
        OpenReleasesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.ReleasesUrl));
        OpenIssuesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.IssuesUrl));
        CopyEmailCommand = new RelayCommand(() => ShellService.CopyText("lucas6.zju@vip.163.com"));
        ReinstallChromeCommand = new RelayCommand(() => { _reinstallChrome(); RefreshChromeStatus(); });

        RefreshChromeStatus();
        UpdateStatusText = L10n.Format("MsgCurrentVersion", _updateService.CurrentVersion);
    }

    public IReadOnlyList<(string Code, string NativeName)> Languages { get; }

    public (string Code, string NativeName) SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value.Code != L10n.CurrentLanguage)
            {
                L10n.SetLanguage(value.Code);
                _profileManager.Config.Language = value.Code;
                _profileManager.Save();
                RefreshChromeStatus();
                UpdateStatusText = L10n.Format("MsgCurrentVersion", _updateService.CurrentVersion);
            }
        }
    }

    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenProfilesFolderCommand { get; }
    public RelayCommand OpenChromeFolderCommand { get; }
    public RelayCommand CopyDataPathCommand { get; }
    public RelayCommand CheckUpdatesCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public RelayCommand OpenIssuesCommand { get; }
    public RelayCommand CopyEmailCommand { get; }
    public RelayCommand ReinstallChromeCommand { get; }

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

    public bool ShowAdvancedDetails
    {
        get => _showAdvancedDetails;
        set
        {
            if (SetProperty(ref _showAdvancedDetails, value))
            {
                _profileManager.Config.ShowAdvancedDetails = value;
                _profileManager.Save();
            }
        }
    }

    private async void CheckForUpdates()
    {
        try
        {
            IsCheckingForUpdates = true;
            UpdateStatusText = L10n.GetString("MsgCheckingUpdate");
            var result = await _updateService.CheckForUpdatesAsync();
            UpdateStatusText = result.Status switch
            {
                UpdateCheckStatus.UpToDate => L10n.Format("MsgUpToDateShort", result.LatestVersion ?? CurrentVersion),
                UpdateCheckStatus.UpdateAvailable => L10n.Format("MsgUpdateAvailable", result.LatestVersion ?? "-"),
                _ => L10n.Format("MsgUpdateFailedShort", result.ErrorMessage ?? "-")
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
            ? L10n.GetString("ChromeNotFoundShort")
            : $"{L10n.Format("ChromeAvailable", chrome.Version ?? "-", chrome.Source)}\n{chrome.ExecutablePath}";
    }
}
