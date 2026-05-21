using ChromeIsolator.Models;
using Microsoft.VisualBasic.FileIO;

namespace ChromeIsolator.Services;

public sealed class ProfileManager
{
    private readonly ConfigStore _configStore;

    public ProfileManager(ConfigStore configStore)
    {
        _configStore = configStore;
        Config = _configStore.Load();
        EnsureProfileDirectories();
    }

    public AppConfig Config { get; }

    public Profile AddProfile()
    {
        var maxNumber = Config.Profiles
            .Select(profile => profile.InstanceNumber)
            .DefaultIfEmpty(0)
            .Max();

        var profile = new Profile { Folder = $"p{maxNumber + 1}" };
        Config.Profiles.Add(profile);
        Directory.CreateDirectory(AppPaths.ProfileDir(profile.Folder));
        Save();
        return profile;
    }

    public void RenameProfile(Profile profile, string displayName)
    {
        profile.DisplayName = displayName.Trim();
        Save();
    }

    public void MoveProfileToRecycleBin(Profile profile)
    {
        var path = AppPaths.ProfileDir(profile.Folder);
        if (Directory.Exists(path))
        {
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        Config.Profiles.Remove(profile);
        Save();
    }

    public void EnsureProfileDirectories()
    {
        AppPaths.EnsureDirectories();
        foreach (var profile in Config.Profiles)
        {
            Directory.CreateDirectory(AppPaths.ProfileDir(profile.Folder));
        }
    }

    public void Save() => _configStore.Save(Config);
}
