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
        ReconcileProfilesWithDisk(File.Exists(AppPaths.ConfigFile));
        EnsureProfileDirectories();
    }

    public AppConfig Config { get; }

    public Profile AddProfile()
    {
        var usedNumbers = Config.Profiles
            .Select(profile => profile.InstanceNumber)
            .Where(number => number > 0)
            .ToHashSet();
        var nextNumber = 1;
        while (usedNumbers.Contains(nextNumber))
        {
            nextNumber++;
        }

        var profile = new Profile { Folder = $"p{nextNumber}" };
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

    private void ReconcileProfilesWithDisk(bool configExists)
    {
        AppPaths.EnsureDirectories();

        var diskFolders = Directory
            .EnumerateDirectories(AppPaths.ProfilesDir)
            .Select(Path.GetFileName)
            .Where(folder => !string.IsNullOrWhiteSpace(folder) && IsProfileFolder(folder))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (diskFolders.Count == 0)
        {
            if (configExists && Config.Profiles.Count > 0)
            {
                Config.Profiles.Clear();
                Save();
            }

            return;
        }

        var changed = false;
        var beforeCount = Config.Profiles.Count;
        Config.Profiles.RemoveAll(profile => !diskFolders.Contains(profile.Folder));
        changed = changed || beforeCount != Config.Profiles.Count;

        var uniqueProfiles = Config.Profiles
            .GroupBy(profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (uniqueProfiles.Count != Config.Profiles.Count)
        {
            Config.Profiles.Clear();
            Config.Profiles.AddRange(uniqueProfiles);
            changed = true;
        }

        var configuredFolders = Config.Profiles
            .Select(profile => profile.Folder)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in diskFolders.Where(folder => !configuredFolders.Contains(folder)))
        {
            Config.Profiles.Add(new Profile { Folder = folder });
            changed = true;
        }

        var sorted = Config.Profiles
            .OrderBy(profile => profile.InstanceNumber == 0 ? int.MaxValue : profile.InstanceNumber)
            .ThenBy(profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!Config.Profiles.SequenceEqual(sorted))
        {
            Config.Profiles.Clear();
            Config.Profiles.AddRange(sorted);
            changed = true;
        }

        if (changed)
        {
            Save();
        }
    }

    private static bool IsProfileFolder(string folder)
    {
        return folder.Length > 1 &&
            folder[0] is 'p' or 'P' &&
            int.TryParse(folder[1..], out var number) &&
            number > 0;
    }
}
