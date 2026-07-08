using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using ChromeIsolator.Models;
using ChromeIsolator.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace ChromeIsolator.ViewModels;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ChromeManager _chromeManager;
    private readonly UpdateService _updateService;
    private readonly ProfileManager _profileManager;
    private readonly Action _reinstallChrome;
    private string _chromeStatusText = "";
    private string _updateStatusText = "";
    private bool _isCheckingForUpdates;
    private bool _showAdvancedDetails;
    private L10n.LanguageOption _selectedLanguage;
    private SettingsProfileOptionViewModel? _selectedExternalLinkProfile;
    private string _profileModeSearchText = "";
    private bool _showOnlyEditableModes;
    private bool _disposed;

    public SettingsViewModel(ChromeManager chromeManager, UpdateService updateService, ProfileManager profileManager, Action reinstallChrome)
    {
        _chromeManager = chromeManager;
        _updateService = updateService;
        _profileManager = profileManager;
        _reinstallChrome = reinstallChrome;
        _showAdvancedDetails = profileManager.Config.ShowAdvancedDetails;
        _chromeManager.ProfileExited += OnProfileExited;

        Languages = L10n.SupportedLanguages;
        ProfileModes = _profileManager.Config.Profiles
            .Select(profile => new SettingsProfileModeViewModel(profile, _profileManager, _chromeManager))
            .ToList();
        foreach (var profileMode in ProfileModes)
        {
            profileMode.PropertyChanged += OnProfileModeChanged;
        }
        ProfileModeView = CollectionViewSource.GetDefaultView(ProfileModes);
        ProfileModeView.Filter = FilterProfileMode;
        ExternalLinkProfiles =
        [
            SettingsProfileOptionViewModel.CreateAutomatic(),
            .. _profileManager.Config.Profiles
            .OrderBy(profile => profile.InstanceNumber == 0 ? int.MaxValue : profile.InstanceNumber)
            .ThenBy(profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SettingsProfileOptionViewModel(profile))
        ];
        _selectedExternalLinkProfile = ResolveSelectedExternalLinkProfile();
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == L10n.CurrentLanguage) ?? Languages[0];

        OpenDataFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.SupportDir));
        OpenProfilesFolderCommand = new RelayCommand(() => ShellService.OpenFolder(AppPaths.ProfilesDir));
        OpenChromeFolderCommand = new RelayCommand(OpenChromeFolder);
        CopyDataPathCommand = new RelayCommand(() => ShellService.CopyText(AppPaths.SupportDir));
        CheckUpdatesCommand = new RelayCommand(CheckForUpdates, () => !IsCheckingForUpdates);
        OpenReleasesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.LatestReleaseUrl));
        OpenIssuesCommand = new RelayCommand(() => ShellService.OpenUrl(UpdateService.IssuesUrl));
        CopyEmailCommand = new RelayCommand(() => ShellService.CopyText(ContactEmail));
        ReinstallChromeCommand = new RelayCommand(() => { _reinstallChrome(); RefreshChromeStatus(); });
        SetDefaultBrowserCommand = new RelayCommand(RequestDefaultBrowser);
        EnableCollectorDebugForEditableCommand = new RelayCommand(() => SetCollectorDebugForEditable(true));
        DisableCollectorDebugForEditableCommand = new RelayCommand(() => SetCollectorDebugForEditable(false));

        RefreshChromeStatus();
        UpdateStatusText = L10n.Format("MsgCurrentVersion", _updateService.CurrentVersion);
    }

    public IReadOnlyList<L10n.LanguageOption> Languages { get; }
    public IReadOnlyList<SettingsProfileModeViewModel> ProfileModes { get; }
    public ICollectionView ProfileModeView { get; }
    public IReadOnlyList<SettingsProfileOptionViewModel> ExternalLinkProfiles { get; }

    public L10n.LanguageOption SelectedLanguage
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
                foreach (var profileMode in ProfileModes)
                {
                    profileMode.RefreshLocalizedProperties();
                }
                OnPropertyChanged(nameof(EnvironmentModeSummary));
                ProfileModeView.Refresh();
                foreach (var profile in ExternalLinkProfiles)
                {
                    profile.RefreshLocalizedProperties();
                }
            }
        }
    }

    public SettingsProfileOptionViewModel? SelectedExternalLinkProfile
    {
        get => _selectedExternalLinkProfile;
        set
        {
            if (SetProperty(ref _selectedExternalLinkProfile, value))
            {
                _profileManager.SetExternalLinkProfile(value?.IsAutomatic == true ? null : value?.Model);
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
    public RelayCommand SetDefaultBrowserCommand { get; }
    public RelayCommand EnableCollectorDebugForEditableCommand { get; }
    public RelayCommand DisableCollectorDebugForEditableCommand { get; }

    public string DataPath => AppPaths.SupportDir;
    public string ProfilesPath => AppPaths.ProfilesDir;
    public string ChromePath => AppPaths.ChromeDir;
    public string AuthorName => "Lucas";
    public string ContactEmail => "lucas6.zju@vip.163.com";
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

    public string EnvironmentModeSummary =>
        L10n.Format(
            "EnvironmentModeSummary",
            ProfileModes.Count(profile => profile.EnableCollectorDebug),
            ProfileModes.Count(profile => profile.EnableEnvironmentVariation),
            ProfileModes.Count);

    public string ProfileModeSearchText
    {
        get => _profileModeSearchText;
        set
        {
            if (SetProperty(ref _profileModeSearchText, value))
            {
                ProfileModeView.Refresh();
            }
        }
    }

    public bool ShowOnlyEditableModes
    {
        get => _showOnlyEditableModes;
        set
        {
            if (SetProperty(ref _showOnlyEditableModes, value))
            {
                ProfileModeView.Refresh();
            }
        }
    }

    public void RefreshProfileModeStates()
    {
        foreach (var profileMode in ProfileModes)
        {
            profileMode.RefreshModeState();
        }
        ProfileModeView.Refresh();
        OnPropertyChanged(nameof(EnvironmentModeSummary));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _chromeManager.ProfileExited -= OnProfileExited;
        foreach (var profileMode in ProfileModes)
        {
            profileMode.PropertyChanged -= OnProfileModeChanged;
        }
    }

    private void OnProfileExited(string _)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_disposed)
                {
                    RefreshProfileModeStates();
                }
            }));
            return;
        }

        RefreshProfileModeStates();
    }

    private void OnProfileModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsProfileModeViewModel.EnableEnvironmentVariation)
            || e.PropertyName == nameof(SettingsProfileModeViewModel.EnableCollectorDebug))
        {
            OnPropertyChanged(nameof(EnvironmentModeSummary));
        }
    }

    private bool FilterProfileMode(object item)
    {
        if (item is not SettingsProfileModeViewModel profileMode)
        {
            return false;
        }

        if (ShowOnlyEditableModes && !profileMode.CanChangeMode)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProfileModeSearchText))
        {
            return true;
        }

        return profileMode.Title.Contains(ProfileModeSearchText.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    private void SetCollectorDebugForEditable(bool enabled)
    {
        foreach (var profileMode in ProfileModes.Where(profile => profile.CanChangeMode))
        {
            profileMode.EnableCollectorDebug = enabled;
        }

        OnPropertyChanged(nameof(EnvironmentModeSummary));
        ProfileModeView.Refresh();
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
                UpdateCheckStatus.UpdateAvailable => L10n.Format("MsgUpdateAvailableShort", CurrentVersion, result.LatestVersion ?? "-"),
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

    private void OpenChromeFolder()
    {
        var chrome = _chromeManager.CurrentChrome;
        if (chrome is not null)
        {
            ShellService.OpenFolder(Path.GetDirectoryName(chrome.ExecutablePath) ?? AppPaths.SupportDir);
            return;
        }

        ShellService.OpenFolder(AppPaths.SupportDir);
    }

    private void RequestDefaultBrowser()
    {
        try
        {
            ShellService.RequestDefaultBrowser();
            WpfMessageBox.Show(
                L10n.GetString("MsgDefaultBrowserRequestOpened"),
                L10n.GetString("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                L10n.Format("MsgDefaultBrowserRequestFailed", ex.Message),
                L10n.GetString("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private SettingsProfileOptionViewModel? ResolveSelectedExternalLinkProfile()
    {
        if (ExternalLinkProfiles.Count == 0)
        {
            return null;
        }

        var configuredFolder = _profileManager.Config.ExternalLinkProfileFolder;
        if (!string.IsNullOrWhiteSpace(configuredFolder))
        {
            var configured = ExternalLinkProfiles.FirstOrDefault(profile =>
                !profile.IsAutomatic &&
                string.Equals(profile.Folder, configuredFolder, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
            {
                return configured;
            }
        }

        return ExternalLinkProfiles.FirstOrDefault(profile => profile.IsAutomatic);
    }
}

public sealed class SettingsProfileOptionViewModel : ObservableObject
{
    private SettingsProfileOptionViewModel(Profile? model, bool isAutomatic)
    {
        Model = model;
        IsAutomatic = isAutomatic;
    }

    public SettingsProfileOptionViewModel(Profile model)
        : this(model, false)
    {
    }

    public static SettingsProfileOptionViewModel CreateAutomatic() => new(null, true);

    public Profile? Model { get; }
    public bool IsAutomatic { get; }
    public string Folder => Model?.Folder ?? "";

    public string Title
    {
        get
        {
            if (IsAutomatic || Model is null)
            {
                return L10n.GetString("ExternalLinkAutoTarget");
            }

            var defaultName = string.Format(L10n.GetString("LabelFolder") == "Folder" ? "Profile {0}" : "环境{0}", Model.InstanceNumber);
            return string.IsNullOrWhiteSpace(Model.DisplayName)
                ? defaultName
                : $"{defaultName} - {Model.DisplayName}";
        }
    }

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Title));
    }
}

public sealed class SettingsProfileModeViewModel : ObservableObject
{
    private readonly Profile _profile;
    private readonly ProfileManager _profileManager;
    private readonly ChromeManager _chromeManager;

    public SettingsProfileModeViewModel(Profile profile, ProfileManager profileManager, ChromeManager chromeManager)
    {
        _profile = profile;
        _profileManager = profileManager;
        _chromeManager = chromeManager;
    }

    public string Title
    {
        get
        {
            var defaultName = string.Format(L10n.GetString("LabelFolder") == "Folder" ? "Profile {0}" : "环境{0}", _profile.InstanceNumber);
            return string.IsNullOrWhiteSpace(_profile.DisplayName)
                ? defaultName
                : $"{defaultName} - {_profile.DisplayName}";
        }
    }

    public bool IsRunning => _chromeManager.IsRunning(_profile);
    public bool CanChangeMode => !IsRunning;

    public bool EnableEnvironmentVariation
    {
        get => _profile.EnableEnvironmentVariation;
        set
        {
            if (IsRunning || _profile.EnableEnvironmentVariation == value)
            {
                return;
            }

            _profile.EnableEnvironmentVariation = value;
            _profileManager.Save();
            OnPropertyChanged();
        }
    }

    public bool EnableCollectorDebug
    {
        get => _profile.EnableCollectorDebug;
        set
        {
            if (IsRunning || _profile.EnableCollectorDebug == value)
            {
                return;
            }

            _profile.EnableCollectorDebug = value;
            _profileManager.Save();
            OnPropertyChanged();
        }
    }

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Title));
    }

    public void RefreshModeState()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanChangeMode));
    }
}
