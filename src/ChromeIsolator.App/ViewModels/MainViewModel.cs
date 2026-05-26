using System.Collections.ObjectModel;
using System.Windows;
using ChromeIsolator.Models;
using ChromeIsolator.Services;
using WpfApplication = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;
using DownloadWindow = ChromeIsolator.DownloadWindow;

namespace ChromeIsolator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;
    private readonly ChromeManager _chromeManager;
    private readonly UpdateService _updateService;
    private ProfileViewModel? _selectedProfile;
    private string _chromeStatusText = "";
    private bool _showAdvancedDetails;
    private bool _isShuttingDown;
    private bool _isBulkStopping;
    private readonly Dictionary<string, List<string>> _pendingExternalUrls = [];

    public MainViewModel(ProfileManager profileManager, ChromeManager chromeManager, UpdateService updateService)
    {
        _profileManager = profileManager;
        _chromeManager = chromeManager;
        _updateService = updateService;

        Profiles = new ObservableCollection<ProfileViewModel>(
            _profileManager.Config.Profiles.Select(profile => new ProfileViewModel(profile)));

        _showAdvancedDetails = _profileManager.Config.ShowAdvancedDetails;

        AddProfileCommand = new RelayCommand(AddProfile);
        StartSelectedCommand = new RelayCommand(StartSelected, CanStartSelectedCheck);
        StopSelectedCommand = new RelayCommand(StopSelected, CanStopSelectedCheck);
        StopAllCommand = new RelayCommand(() => _ = StopAllSafeAsync());
        PrepareChromeCommand = new RelayCommand(PrepareChrome);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        RenameSelectedCommand = new RelayCommand(RenameSelected, () => SelectedProfile is not null);
        EditNoteCommand = new RelayCommand(EditSelectedNote, () => SelectedProfile is not null);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedProfile is not null);
        ClearErrorCommand = new RelayCommand(ClearError, () => SelectedProfile is not null && !string.IsNullOrEmpty(SelectedProfile?.Error));
        RetrySelectedCommand = new RelayCommand(RetrySelected, () => SelectedProfile is not null && !SelectedProfile!.IsRunning && !string.IsNullOrEmpty(SelectedProfile?.Error));
        OpenProfileFolderCommand = new RelayCommand(OpenProfileFolder, () => SelectedProfile is not null);
        CopyProfilePathCommand = new RelayCommand(CopyProfilePath, () => SelectedProfile is not null);
        ToggleSelectedCommand = new RelayCommand<ProfileViewModel>(ToggleProfile);
        StartProfileCommand = new RelayCommand<ProfileViewModel>(StartProfileFromList);
        StopProfileCommand = new RelayCommand<ProfileViewModel>(StopProfileFromList);
        SelectedProfile = Profiles.FirstOrDefault();

        _chromeManager.ProfileExited += OnProfileExited;
        L10n.LanguageChanged += OnLanguageChanged;
        RefreshChromeStatus();
        _ = RefreshDiskSizesAsync();
    }

    public ObservableCollection<ProfileViewModel> Profiles { get; }

    public ProfileViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                RaiseCommandState();
                OnPropertyChanged(nameof(SelectedProfileTitle));
            }
        }
    }

    public string SelectedProfileTitle => _selectedProfile?.Title ?? L10n.GetString("SelectProfile");

    public RelayCommand AddProfileCommand { get; }
    public RelayCommand StartSelectedCommand { get; }
    public RelayCommand StopSelectedCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand PrepareChromeCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand RenameSelectedCommand { get; }
    public RelayCommand EditNoteCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand ClearErrorCommand { get; }
    public RelayCommand RetrySelectedCommand { get; }
    public RelayCommand OpenProfileFolderCommand { get; }
    public RelayCommand CopyProfilePathCommand { get; }
    public RelayCommand<ProfileViewModel> ToggleSelectedCommand { get; }
    public RelayCommand<ProfileViewModel> StartProfileCommand { get; }
    public RelayCommand<ProfileViewModel> StopProfileCommand { get; }

    public bool CanStartSelected => CanStartSelectedCheck();
    public bool CanStopSelected => CanStopSelectedCheck();

    public string ChromeStatusText
    {
        get => _chromeStatusText;
        private set => SetProperty(ref _chromeStatusText, value);
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

    public string ChromeVersionText
    {
        get
        {
            var chrome = _chromeManager.CurrentChrome;
            return chrome?.Version ?? "-";
        }
    }

    public string ChromePathText
    {
        get
        {
            var chrome = _chromeManager.CurrentChrome;
            return chrome?.ExecutablePath ?? "-";
        }
    }

    public string CpuCoresText => Environment.ProcessorCount.ToString();
    public string MemoryText
    {
        get
        {
            var gc = GC.GetGCMemoryInfo();
            var totalMb = gc.TotalAvailableMemoryBytes / (1024.0 * 1024.0);
            return $"{totalMb:F0} MB";
        }
    }

    public double? MainWindowLeft => _profileManager.Config.MainWindowLeft;
    public double? MainWindowTop => _profileManager.Config.MainWindowTop;
    public double? MainWindowWidth => _profileManager.Config.MainWindowWidth;
    public double? MainWindowHeight => _profileManager.Config.MainWindowHeight;
    public bool MainWindowIsMaximized => _profileManager.Config.MainWindowIsMaximized;
    public double? MainWindowLeftPaneWidth => _profileManager.Config.MainWindowLeftPaneWidth;

    public void ToggleProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;
        if (profile.IsRunning)
        {
            _chromeManager.BringToFront(profile.Model);
        }
        else if (!profile.IsStarting && !profile.IsStopping)
        {
            StartProfile(profile);
        }
    }

    public void StartProfileFromList(ProfileViewModel? profile)
    {
        if (_isBulkStopping || profile is null || profile.IsRunning || profile.IsStarting || profile.IsStopping)
        {
            return;
        }

        SelectedProfile = profile;
        StartProfile(profile);
    }

    public void StopProfileFromList(ProfileViewModel? profile)
    {
        if (profile is null || !profile.IsRunning || profile.IsStarting || profile.IsStopping)
        {
            return;
        }

        SelectedProfile = profile;
        _ = StopProfileSafeAsync(profile);
    }

    public void StartSelected()
    {
        if (CanStartSelected && SelectedProfile is not null)
        {
            StartProfile(SelectedProfile);
        }
    }

    public async Task StopAllAsync()
    {
        if (_isBulkStopping)
        {
            return;
        }

        _isBulkStopping = true;
        var affectedProfiles = Profiles
            .Where(profile => profile.IsRunning || profile.IsStarting || profile.IsStopping)
            .ToList();
        var profileModels = Profiles.Select(profile => profile.Model).ToList();

        try
        {
            foreach (var profile in affectedProfiles)
            {
                profile.IsStopping = true;
            }
            RaiseCommandState();

            await Task.Run(() => _chromeManager.StopAll(profileModels)).ConfigureAwait(true);

            foreach (var profile in affectedProfiles)
            {
                profile.IsRunning = false;
                profile.IsStarting = false;
                profile.IsStopping = false;
                profile.DebugPort = null;
                MarkProfileUsed(profile);
            }
            _ = RefreshDiskSizesAsync();
            SortProfiles();
            _profileManager.Save();
        }
        catch
        {
            foreach (var profile in affectedProfiles)
            {
                profile.IsStarting = false;
                profile.IsStopping = false;
                profile.IsRunning = _chromeManager.IsRunning(profile.Model);
                profile.DebugPort = _chromeManager.DebugPort(profile.Model);
            }

            throw;
        }
        finally
        {
            _isBulkStopping = false;
            RaiseCommandState();
        }
    }

    internal async Task StopAllSafeAsync()
    {
        try
        {
            await StopAllAsync();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, L10n.GetString("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task StopAllAndQuitAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        try
        {
            _isBulkStopping = true;
            var profileModels = Profiles.Select(profile => profile.Model).ToList();

            foreach (var profile in Profiles)
            {
                if (profile.IsRunning)
                {
                    profile.IsStopping = true;
                }
            }

            await Task.Run(() => _chromeManager.StopAll(profileModels));
            WpfApplication.Current.Shutdown();
        }
        finally
        {
            _isBulkStopping = false;
        }
    }

    private void AddProfile()
    {
        var profile = _profileManager.AddProfile();
        var viewModel = new ProfileViewModel(profile);
        Profiles.Add(viewModel);
        SelectedProfile = viewModel;

        var input = SimpleInputDialog.Show(L10n.GetString("MsgRenameTitle"), L10n.GetString("MsgRenameMessage"), "");
        if (!string.IsNullOrWhiteSpace(input))
        {
            _profileManager.RenameProfile(profile, input);
            viewModel.RefreshTitle();
            OnPropertyChanged(nameof(SelectedProfileTitle));
        }
    }

    private void PrepareChrome()
    {
        var downloadWindow = new DownloadWindow(_chromeManager)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        downloadWindow.ShowDialog();

        ApplyBrowserSetupResult(downloadWindow);

        if (downloadWindow.UseInstalled)
        {
            RefreshChromeStatus();
            if (_chromeManager.CurrentChrome is null)
            {
                WpfMessageBox.Show(
                    L10n.GetString("ChromeNotFound"),
                    L10n.GetString("AppTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        RefreshChromeStatus();
    }

    public void ShowDownloadIfNeeded()
    {
        if (_profileManager.Config.FirstRunCompleted && _chromeManager.CurrentChrome is not null)
        {
            return;
        }

        var downloadWindow = new DownloadWindow(_chromeManager)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        downloadWindow.ShowDialog();

        ApplyBrowserSetupResult(downloadWindow);

        RefreshChromeStatus();
    }

    public async Task CheckForUpdatesFromTrayAsync()
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            var message = result.Status switch
            {
                UpdateCheckStatus.UpToDate => L10n.Format("MsgUpToDate", result.LatestVersion ?? _updateService.CurrentVersion),
                UpdateCheckStatus.UpdateAvailable => L10n.Format("MsgUpdateAvailable", result.LatestVersion ?? "-"),
                _ => L10n.Format("MsgUpdateFailed", result.ErrorMessage ?? "-")
            };

            var icon = result.Status switch
            {
                UpdateCheckStatus.UpToDate => MessageBoxImage.Information,
                UpdateCheckStatus.UpdateAvailable => MessageBoxImage.Question,
                _ => MessageBoxImage.Error
            };

            if (result.Status == UpdateCheckStatus.UpdateAvailable)
            {
                var choice = WpfMessageBox.Show(message, L10n.GetString("AppTitle"), MessageBoxButton.YesNo, icon);
                if (choice == MessageBoxResult.Yes)
                {
                    ShellService.OpenUrl(UpdateService.LatestReleaseUrl);
                }
            }
            else
            {
                WpfMessageBox.Show(message, L10n.GetString("AppTitle"), MessageBoxButton.OK, icon);
            }
        }
        catch
        {
            WpfMessageBox.Show(L10n.GetString("MsgUpdateError"), L10n.GetString("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        void ReinstallChrome()
        {
            var downloadWindow = new DownloadWindow(_chromeManager)
            {
                Owner = WpfApplication.Current.MainWindow
            };
            downloadWindow.ShowDialog();
            ApplyBrowserSetupResult(downloadWindow);
        }

        var window = new SettingsWindow(new SettingsViewModel(_chromeManager, _updateService, _profileManager, ReinstallChrome))
        {
            Owner = WpfApplication.Current.MainWindow
        };
        window.ShowDialog();
        _showAdvancedDetails = _profileManager.Config.ShowAdvancedDetails;
        OnPropertyChanged(nameof(ShowAdvancedDetails));
        foreach (var profile in Profiles)
        {
            profile.RefreshModeInfo();
        }
        RefreshChromeStatus();
    }

    private void StopSelected()
    {
        if (CanStopSelected && SelectedProfile is not null)
        {
            _ = StopProfileSafeAsync(SelectedProfile);
        }
    }

    internal async Task StopProfileSafeAsync(ProfileViewModel profile)
    {
        try
        {
            await StopProfileAsync(profile);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, L10n.GetString("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenameSelected()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var currentName = SelectedProfile.Model.DisplayName;
        var input = SimpleInputDialog.Show(L10n.GetString("MsgRenameTitle"), L10n.GetString("MsgRenameMessage"), currentName);
        if (input is null)
        {
            return;
        }

        _profileManager.RenameProfile(SelectedProfile.Model, input);
        SelectedProfile.RefreshTitle();
        OnPropertyChanged(nameof(SelectedProfileTitle));
    }

    private void EditSelectedNote()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var input = SimpleInputDialog.Show(
            L10n.GetString("MsgEditNoteTitle"),
            L10n.GetString("MsgEditNoteMessage"),
            SelectedProfile.Model.Note,
            maxLength: 120,
            multiline: true);
        if (input is null)
        {
            return;
        }

        _profileManager.UpdateProfileNote(SelectedProfile.Model, input);
        SelectedProfile.RefreshNote();
    }

    private void DeleteSelected()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (SelectedProfile.IsRunning)
        {
            WpfMessageBox.Show(L10n.GetString("MsgRunningEnvNoDelete"), L10n.GetString("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = SimpleInputDialog.Show(
            L10n.GetString("MsgDeleteTitle"),
            L10n.Format("MsgDeleteConfirm", SelectedProfile.Title, SelectedProfile.DiskSizeRaw),
            "");
        if (!string.Equals(confirm, "delete", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var removed = SelectedProfile;
        _profileManager.MoveProfileToRecycleBin(removed.Model);
        Profiles.Remove(removed);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void ClearError()
    {
        if (SelectedProfile is not null)
        {
            SelectedProfile.Error = null;
            RaiseCommandState();
        }
    }

    private void RetrySelected()
    {
        if (SelectedProfile is not null && !SelectedProfile.IsRunning)
        {
            StartProfile(SelectedProfile);
            RaiseCommandState();
        }
    }

    private void OpenProfileFolder()
    {
        if (SelectedProfile is not null)
        {
            ShellService.OpenFolder(AppPaths.ProfileDir(SelectedProfile.Folder));
        }
    }

    private void CopyProfilePath()
    {
        if (SelectedProfile is not null)
        {
            WpfClipboard.SetText(AppPaths.ProfileDir(SelectedProfile.Folder));
        }
    }

    public void StartProfile(ProfileViewModel profile, string? initialUrl = null)
    {
        if (_isBulkStopping)
        {
            return;
        }

        try
        {
            profile.Error = null;
            profile.IsStarting = true;
            RaiseCommandState();
            _chromeManager.Start(profile.Model, initialUrl);
            profile.IsStarting = false;
            profile.IsRunning = true;
            profile.DebugPort = _chromeManager.DebugPort(profile.Model);
            MarkProfileUsed(profile);
            OpenPendingExternalUrls(profile);
            _profileManager.Save();
            RaiseCommandState();
            SortProfiles();
        }
        catch (Exception ex)
        {
            profile.IsStarting = false;
            profile.Error = ex.Message;
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                ShowMainWindowForExternalLinkIssue();
                ShowExternalLinkError(L10n.GetString("MsgExternalLinkBrowserNotReady"), initialUrl);
            }
            RefreshChromeStatus();
            RaiseCommandState();
        }
    }

    public void SaveMainWindowPlacement(Window window, double leftPaneWidth)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            return;
        }

        var bounds = window.WindowState == WindowState.Maximized
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        if (!double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top) ||
            !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height))
        {
            return;
        }

        var config = _profileManager.Config;
        config.MainWindowLeft = bounds.Left;
        config.MainWindowTop = bounds.Top;
        config.MainWindowWidth = Math.Max(window.MinWidth, bounds.Width);
        config.MainWindowHeight = Math.Max(window.MinHeight, bounds.Height);
        config.MainWindowIsMaximized = window.WindowState == WindowState.Maximized;
        if (double.IsFinite(leftPaneWidth) && leftPaneWidth > 0)
        {
            config.MainWindowLeftPaneWidth = leftPaneWidth;
        }

        _profileManager.Save();
    }

    public void HandleExternalLink(string url)
    {
        if (!IsHttpUrl(url))
        {
            ShowExternalLinkError(L10n.GetString("MsgExternalLinkInvalid"), url);
            return;
        }

        var target = ResolveExternalLinkProfile();
        if (target is null)
        {
            ShowMainWindowForExternalLinkIssue();
            ShowExternalLinkError(L10n.GetString("MsgExternalLinkNoProfiles"), url);
            return;
        }

        if (_chromeManager.CurrentChrome is null)
        {
            HandleBrowserNotReadyExternalLink(url);
            return;
        }

        try
        {
            if (target.IsStarting)
            {
                QueueExternalUrl(target.Folder, url);
                return;
            }

            if (target.IsRunning)
            {
                _chromeManager.OpenUrl(target.Model, url);
                MarkProfileUsed(target);
                _profileManager.Save();
                SortProfiles();
                return;
            }

            StartProfile(target, url);
        }
        catch
        {
            ShowMainWindowForExternalLinkIssue();
            ShowExternalLinkError(L10n.GetString("MsgExternalLinkBrowserNotReady"), url);
        }
    }

    public async Task StopProfileAsync(ProfileViewModel profile)
    {
        profile.IsStopping = true;
        RaiseCommandState();

        try
        {
            await Task.Run(() => _chromeManager.Stop(profile.Model)).ConfigureAwait(true);

            profile.IsRunning = false;
            profile.DebugPort = null;
            MarkProfileUsed(profile);
            _profileManager.Save();
            _ = profile.RefreshDiskSizeAsync();
            SortProfiles();
        }
        catch
        {
            profile.IsRunning = _chromeManager.IsRunning(profile.Model);
            profile.DebugPort = _chromeManager.DebugPort(profile.Model);
            throw;
        }
        finally
        {
            profile.IsStopping = false;
            RaiseCommandState();
        }
    }

    private void OnProfileExited(string folder)
    {
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            var profile = Profiles.FirstOrDefault(item => item.Folder == folder);
            if (profile is null)
            {
                return;
            }

            profile.IsRunning = false;
            profile.IsStarting = false;
            profile.IsStopping = false;
            profile.DebugPort = null;
            MarkProfileUsed(profile);
            _ = profile.RefreshDiskSizeAsync();
            RaiseCommandState();
            SortProfiles();
            _profileManager.Save();
        });
    }

    private ProfileViewModel? ResolveExternalLinkProfile()
    {
        if (Profiles.Count == 0)
        {
            return null;
        }

        var configuredFolder = _profileManager.Config.ExternalLinkProfileFolder;
        if (!string.IsNullOrWhiteSpace(configuredFolder))
        {
            var configured = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Folder, configuredFolder, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
            {
                return configured;
            }
        }

        return Profiles
            .OrderBy(profile => profile.Model.InstanceNumber == 0 ? int.MaxValue : profile.Model.InstanceNumber)
            .ThenBy(profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private void QueueExternalUrl(string folder, string url)
    {
        if (!_pendingExternalUrls.TryGetValue(folder, out var urls))
        {
            urls = [];
            _pendingExternalUrls[folder] = urls;
        }

        urls.Add(url);
    }

    private void OpenPendingExternalUrls(ProfileViewModel profile)
    {
        if (!_pendingExternalUrls.Remove(profile.Folder, out var urls))
        {
            return;
        }

        foreach (var url in urls)
        {
            try
            {
                _chromeManager.OpenUrl(profile.Model, url);
            }
            catch
            {
                ShowExternalLinkError(L10n.GetString("MsgExternalLinkBrowserNotReady"), url);
            }
        }
    }

    private static bool IsHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";
    }

    private static void ShowExternalLinkError(string message, string url)
    {
        SimpleInputDialog.ShowCopyMessage(L10n.GetString("ExternalLinkTitle"), message, url);
    }

    private void HandleBrowserNotReadyExternalLink(string url)
    {
        ShowMainWindowForExternalLinkIssue();
        ShowExternalLinkError(L10n.GetString("MsgExternalLinkBrowserNotReady"), url);
        ShowDownloadIfNeeded();

        if (_chromeManager.CurrentChrome is not null)
        {
            HandleExternalLink(url);
        }
    }

    private static void ShowMainWindowForExternalLinkIssue()
    {
        var mainWindow = WpfApplication.Current.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }
        mainWindow.Activate();
    }

    private void OnLanguageChanged()
    {
        RefreshChromeStatus();
        foreach (var profile in Profiles)
        {
            profile.RefreshLocalizedProperties();
        }
        OnPropertyChanged(nameof(ChromeVersionText));
        OnPropertyChanged(nameof(ChromePathText));
        OnPropertyChanged(nameof(SelectedProfileTitle));
    }

    private void MarkProfileUsed(ProfileViewModel profile)
    {
        profile.LastUsed = DateTime.Now;
    }

    private void RaiseCommandState()
    {
        StartSelectedCommand.RaiseCanExecuteChanged();
        StopSelectedCommand.RaiseCanExecuteChanged();
        RenameSelectedCommand.RaiseCanExecuteChanged();
        EditNoteCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        ClearErrorCommand.RaiseCanExecuteChanged();
        RetrySelectedCommand.RaiseCanExecuteChanged();
        OpenProfileFolderCommand.RaiseCanExecuteChanged();
        CopyProfilePathCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanStopSelected));
    }

    private bool CanStartSelectedCheck()
    {
        return SelectedProfile is not null
            && !_isBulkStopping
            && !SelectedProfile.IsRunning
            && !SelectedProfile.IsStarting
            && !SelectedProfile.IsStopping;
    }

    private bool CanStopSelectedCheck()
    {
        return SelectedProfile is not null
            && SelectedProfile.IsRunning
            && !SelectedProfile.IsStarting
            && !SelectedProfile.IsStopping;
    }

    private void RefreshChromeStatus()
    {
        var chrome = _chromeManager.CurrentChrome;
        ChromeStatusText = chrome is null
            ? L10n.GetString("ChromeNotFoundShort")
            : L10n.Format("ChromeAvailable", chrome.Version ?? "-", chrome.Source);
    }

    private void ApplyBrowserSetupResult(DownloadWindow downloadWindow)
    {
        if (downloadWindow.UseEdgeFallback)
        {
            _profileManager.Config.AllowEdgeFallback = true;
            _profileManager.Config.FirstRunCompleted = true;
            _profileManager.Save();
        }
        else if (downloadWindow.UseInstalled || downloadWindow.DownloadSucceeded)
        {
            _profileManager.Config.FirstRunCompleted = true;
            _profileManager.Save();
        }
    }

    private async Task RefreshDiskSizesAsync()
    {
        foreach (var profile in Profiles)
        {
            await profile.RefreshDiskSizeAsync();
        }
    }

    private void SortProfiles()
    {
        var selected = SelectedProfile;
        var sorted = Profiles
            .OrderByDescending(p => p.IsRunning)
            .ThenByDescending(p => p.LastUsed ?? DateTime.MinValue)
            .ToList();

        var changed = false;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (Profiles[i] != sorted[i])
            {
                changed = true;
                break;
            }
        }

        if (!changed) return;

        Profiles.Clear();
        foreach (var item in sorted)
        {
            Profiles.Add(item);
        }

        SelectedProfile = selected is not null && Profiles.Contains(selected) ? selected : Profiles.FirstOrDefault();
    }
}
