using System.Collections.ObjectModel;
using System.Windows;
using ChromeIsolator.Models;
using ChromeIsolator.Services;

namespace ChromeIsolator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;
    private readonly ChromeManager _chromeManager;
    private ProfileViewModel? _selectedProfile;

    public MainViewModel(ProfileManager profileManager, ChromeManager chromeManager)
    {
        _profileManager = profileManager;
        _chromeManager = chromeManager;

        Profiles = new ObservableCollection<ProfileViewModel>(
            _profileManager.Config.Profiles.Select(profile => new ProfileViewModel(profile)));
        SelectedProfile = Profiles.FirstOrDefault();

        AddProfileCommand = new RelayCommand(AddProfile);
        StartSelectedCommand = new RelayCommand(StartSelected, () => SelectedProfile is not null);
        StopSelectedCommand = new RelayCommand(StopSelected, () => SelectedProfile is not null);
        StopAllCommand = new RelayCommand(StopAll);
        RenameSelectedCommand = new RelayCommand(RenameSelected, () => SelectedProfile is not null);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedProfile is not null);

        _chromeManager.ProfileExited += OnProfileExited;
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
    public RelayCommand RenameSelectedCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }

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
            MessageBox.Show("运行中的环境不能删除，请先关闭。", "浏览器多开", MessageBoxButton.OK, MessageBoxImage.Information);
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
        Application.Current.Dispatcher.Invoke(() =>
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
}
