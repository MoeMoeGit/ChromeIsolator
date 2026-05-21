using System.Collections.ObjectModel;
using System.Windows;
using ChromeIsolator.Models;
using ChromeIsolator.Services;
using WpfApplication = System.Windows.Application;
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

    public MainViewModel(ProfileManager profileManager, ChromeManager chromeManager, UpdateService updateService)
    {
        _profileManager = profileManager;
        _chromeManager = chromeManager;
        _updateService = updateService;

        Profiles = new ObservableCollection<ProfileViewModel>(
            _profileManager.Config.Profiles.Select(profile => new ProfileViewModel(profile)));
        SelectedProfile = Profiles.FirstOrDefault();

        _showAdvancedDetails = _profileManager.Config.ShowAdvancedDetails;

        AddProfileCommand = new RelayCommand(AddProfile);
        StartSelectedCommand = new RelayCommand(StartSelected, () => SelectedProfile is not null);
        StopSelectedCommand = new RelayCommand(StopSelected, () => SelectedProfile is not null);
        StopAllCommand = new RelayCommand(StopAll);
        PrepareChromeCommand = new RelayCommand(PrepareChrome);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        RenameSelectedCommand = new RelayCommand(RenameSelected, () => SelectedProfile is not null);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedProfile is not null);
        ClearErrorCommand = new RelayCommand(ClearError, () => SelectedProfile is not null && !string.IsNullOrEmpty(SelectedProfile?.Error));
        RetrySelectedCommand = new RelayCommand(RetrySelected, () => SelectedProfile is not null && !SelectedProfile!.IsRunning && !string.IsNullOrEmpty(SelectedProfile?.Error));

        _chromeManager.ProfileExited += OnProfileExited;
        L10n.LanguageChanged += OnLanguageChanged;
        RefreshChromeStatus();
        RefreshDiskSizes();
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
            }
        }
    }

    public RelayCommand AddProfileCommand { get; }
    public RelayCommand StartSelectedCommand { get; }
    public RelayCommand StopSelectedCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand PrepareChromeCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand RenameSelectedCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand ClearErrorCommand { get; }
    public RelayCommand RetrySelectedCommand { get; }

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

    public void ToggleProfile(ProfileViewModel profile)
    {
        if (profile.IsRunning)
        {
            StopProfile(profile);
        }
        else
        {
            StartProfile(profile);
        }
    }

    public void StartSelected()
    {
        if (SelectedProfile is not null && !SelectedProfile.IsRunning)
        {
            StartProfile(SelectedProfile);
        }
    }

    public void StopAll()
    {
        _chromeManager.StopAll(Profiles.Select(profile => profile.Model));
        foreach (var profile in Profiles)
        {
            profile.IsRunning = false;
            profile.DebugPort = null;
            profile.LastUsed = DateTime.Now;
            profile.RefreshDiskSize();
        }
        SortProfiles();
    }

    private void AddProfile()
    {
        var profile = _profileManager.AddProfile();
        var viewModel = new ProfileViewModel(profile);
        Profiles.Add(viewModel);
        SelectedProfile = viewModel;
    }

    private void PrepareChrome()
    {
        var downloadWindow = new DownloadWindow(_chromeManager)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        downloadWindow.ShowDialog();

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
        if (_chromeManager.CurrentChrome is not null)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            L10n.GetString("MsgChromeNotFound"),
            L10n.GetString("AppTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            PrepareChrome();
        }
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
                    ShellService.OpenUrl(UpdateService.ReleasesUrl);
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
        }

        var window = new SettingsWindow(new SettingsViewModel(_chromeManager, _updateService, _profileManager, ReinstallChrome))
        {
            Owner = WpfApplication.Current.MainWindow
        };
        window.ShowDialog();
        _showAdvancedDetails = _profileManager.Config.ShowAdvancedDetails;
        OnPropertyChanged(nameof(ShowAdvancedDetails));
        RefreshChromeStatus();
    }

    private void StopSelected()
    {
        if (SelectedProfile is not null)
        {
            StopProfile(SelectedProfile);
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

        var confirm = SimpleInputDialog.Show(L10n.GetString("MsgDeleteTitle"), L10n.Format("MsgDeleteConfirm", SelectedProfile.Title), "");
        if (confirm != SelectedProfile.Title)
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

    private void StartProfile(ProfileViewModel profile)
    {
        try
        {
            profile.Error = null;
            _chromeManager.Start(profile.Model);
            profile.IsRunning = true;
            profile.DebugPort = _chromeManager.DebugPort(profile.Model);
            profile.LastUsed = DateTime.Now;
            SortProfiles();
        }
        catch (Exception ex)
        {
            profile.Error = ex.Message;
            RefreshChromeStatus();
        }
    }

    private void StopProfile(ProfileViewModel profile)
    {
        _chromeManager.Stop(profile.Model);
        profile.IsRunning = false;
        profile.DebugPort = null;
        profile.LastUsed = DateTime.Now;
        profile.RefreshDiskSize();
        SortProfiles();
    }

    private void OnProfileExited(string folder)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var profile = Profiles.FirstOrDefault(item => item.Folder == folder);
            if (profile is null)
            {
                return;
            }

            profile.IsRunning = false;
            profile.DebugPort = null;
            profile.LastUsed = DateTime.Now;
            profile.RefreshDiskSize();
            SortProfiles();
        });
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
    }

    private void RaiseCommandState()
    {
        StartSelectedCommand.RaiseCanExecuteChanged();
        StopSelectedCommand.RaiseCanExecuteChanged();
        RenameSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        ClearErrorCommand.RaiseCanExecuteChanged();
        RetrySelectedCommand.RaiseCanExecuteChanged();
    }

    private void RefreshChromeStatus()
    {
        var chrome = _chromeManager.CurrentChrome;
        ChromeStatusText = chrome is null
            ? L10n.GetString("ChromeNotFoundShort")
            : L10n.Format("ChromeAvailable", chrome.Version ?? "-", chrome.Source);
    }

    private void RefreshDiskSizes()
    {
        foreach (var profile in Profiles)
        {
            profile.RefreshDiskSize();
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
