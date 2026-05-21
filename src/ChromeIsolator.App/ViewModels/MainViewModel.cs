using System.Collections.ObjectModel;
using System.Windows;
using ChromeIsolator.Models;
using ChromeIsolator.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace ChromeIsolator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;
    private readonly ChromeManager _chromeManager;
    private readonly UpdateService _updateService;
    private ProfileViewModel? _selectedProfile;
    private string _chromeStatusText = "";
    private bool _isPreparingChrome;

    public MainViewModel(ProfileManager profileManager, ChromeManager chromeManager, UpdateService updateService)
    {
        _profileManager = profileManager;
        _chromeManager = chromeManager;
        _updateService = updateService;

        Profiles = new ObservableCollection<ProfileViewModel>(
            _profileManager.Config.Profiles.Select(profile => new ProfileViewModel(profile)));
        SelectedProfile = Profiles.FirstOrDefault();

        AddProfileCommand = new RelayCommand(AddProfile);
        StartSelectedCommand = new RelayCommand(StartSelected, () => SelectedProfile is not null);
        StopSelectedCommand = new RelayCommand(StopSelected, () => SelectedProfile is not null);
        StopAllCommand = new RelayCommand(StopAll);
        PrepareChromeCommand = new RelayCommand(PrepareChrome, () => !IsPreparingChrome);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        RenameSelectedCommand = new RelayCommand(RenameSelected, () => SelectedProfile is not null);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedProfile is not null);

        _chromeManager.ProfileExited += OnProfileExited;
        RefreshChromeStatus();
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

    public string ChromeStatusText
    {
        get => _chromeStatusText;
        private set => SetProperty(ref _chromeStatusText, value);
    }

    public bool IsPreparingChrome
    {
        get => _isPreparingChrome;
        private set
        {
            if (SetProperty(ref _isPreparingChrome, value))
            {
                PrepareChromeCommand.RaiseCanExecuteChanged();
            }
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
        if (SelectedProfile is not null)
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
        }
    }

    private void AddProfile()
    {
        var profile = _profileManager.AddProfile();
        var viewModel = new ProfileViewModel(profile);
        Profiles.Add(viewModel);
        SelectedProfile = viewModel;
    }

    private async void PrepareChrome()
    {
        try
        {
            IsPreparingChrome = true;
            ChromeStatusText = "Chrome 准备中...";
            await _chromeManager.PrepareChromeAsync();
            RefreshChromeStatus();
        }
        catch (Exception ex)
        {
            ChromeStatusText = $"Chrome 准备失败：{ex.Message}";
        }
        finally
        {
            IsPreparingChrome = false;
        }
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(new SettingsViewModel(_chromeManager, _updateService))
        {
            Owner = WpfApplication.Current.MainWindow
        };
        window.ShowDialog();
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
        var input = SimpleInputDialog.Show("重命名", "自定义名称，方便识别", currentName);
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
            WpfMessageBox.Show("运行中的环境不能删除，请先关闭。", "浏览器多开", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = SimpleInputDialog.Show("确认删除", $"将把“{SelectedProfile.Title}”的数据移到回收站。\n请输入环境名称确认。", "");
        if (confirm != SelectedProfile.Title)
        {
            return;
        }

        var removed = SelectedProfile;
        _profileManager.MoveProfileToRecycleBin(removed.Model);
        Profiles.Remove(removed);
        SelectedProfile = Profiles.FirstOrDefault();
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
        });
    }

    private void RaiseCommandState()
    {
        StartSelectedCommand.RaiseCanExecuteChanged();
        StopSelectedCommand.RaiseCanExecuteChanged();
        RenameSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
    }

    private void RefreshChromeStatus()
    {
        var chrome = _chromeManager.CurrentChrome;
        ChromeStatusText = chrome is null
            ? "Chrome 未找到"
            : $"Chrome 可用：{chrome.Version ?? "未知版本"}（{chrome.Source}）";
    }
}
