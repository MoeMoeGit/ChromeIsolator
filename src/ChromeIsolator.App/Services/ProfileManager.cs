using ChromeIsolator.Models;
using Microsoft.VisualBasic.FileIO;

namespace ChromeIsolator.Services;

public sealed class ProfileManager
{
    private readonly ConfigStore _configStore;

    private readonly Action<string> _recycleDirectory;

    public ProfileManager(ConfigStore configStore, Action<string>? recycleDirectory = null)
    {
        _configStore = configStore;
        _recycleDirectory = recycleDirectory ?? (path => FileSystem.DeleteDirectory(
            path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin));
        Config = _configStore.Load();
        RecoveredConfigFromBackup = _configStore.RecoveredFromBackup;
        UsedDefaultConfigAfterLoadFailure = _configStore.UsedDefaultAfterLoadFailure;
        var reconciled = ReconcileProfilesWithDisk(_configStore.LoadedExistingConfig);
        if (reconciled || !_configStore.LoadedExistingConfig) Save();
        EnsureProfileDirectories();
    }

    public AppConfig Config { get; }
    public event Action<Exception>? SaveFailed;
    public Exception? LastSaveError { get; private set; }
    public bool RebuiltConfigFromDisk { get; private set; }
    public bool RecoveredConfigFromBackup { get; }
    public bool UsedDefaultConfigAfterLoadFailure { get; }

    public Profile AddProfile()
    {
        var nextNumber = GetNextAvailableProfileNumber();

        var profile = Profile.NewEnvironment($"p{nextNumber}");
        Directory.CreateDirectory(AppPaths.ProfileDir(profile.Folder));
        Config.Profiles.Add(profile);
        Save();
        return profile;
    }

    public void RenameProfile(Profile profile, string displayName)
    {
        profile.DisplayName = displayName.Trim();
        Save();
    }

    public void UpdateProfileNote(Profile profile, string note)
    {
        profile.Note = TrimNote(note);
        Save();
    }

    public void SetExternalLinkProfile(Profile? profile)
    {
        Config.ExternalLinkProfileFolder = profile?.Folder;
        Save();
    }

    public bool MoveProfileToRecycleBin(Profile profile)
    {
        var originalProfiles = Config.Profiles.ToList();
        var originalTarget = Config.ExternalLinkProfileFolder;
        var folder = profile.Folder;
        Config.Profiles.RemoveAll(item => string.Equals(item.Folder, folder, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(originalTarget, folder, StringComparison.OrdinalIgnoreCase))
            Config.ExternalLinkProfileFolder = null;

        // Do not remove browser data unless its removal from the configuration is durable.
        if (!Save())
        {
            Config.Profiles = originalProfiles;
            Config.ExternalLinkProfileFolder = originalTarget;
            return false;
        }
        try
        {
            var path = AppPaths.ProfileDir(folder);
            if (Directory.Exists(path)) _recycleDirectory(path);
            return true;
        }
        catch
        {
            Config.Profiles = originalProfiles;
            Config.ExternalLinkProfileFolder = originalTarget;
            Save();
            throw;
        }
    }

    public void EnsureProfileDirectories()
    {
        AppPaths.EnsureDirectories();
        foreach (var profile in Config.Profiles)
        {
            Directory.CreateDirectory(AppPaths.ProfileDir(profile.Folder));
        }
    }

    public bool Save()
    {
        try
        {
            _configStore.Save(Config);
            LastSaveError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastSaveError = ex;
            SaveFailed?.Invoke(ex);
            return false;
        }
    }

    private bool ReconcileProfilesWithDisk(bool configExists)
    {
        AppPaths.EnsureDirectories();

        var diskFolders = Directory
            .EnumerateDirectories(AppPaths.ProfilesDir)
            .Select(Path.GetFileName)
            .Where(folder => !string.IsNullOrWhiteSpace(folder) && IsProfileFolder(folder))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A valid configuration owns names, notes and modes even if a directory was lost.
        // When rebuilding from disk, never apply new-environment defaults to recovered profiles.
        var changed = false;
        if (!configExists && diskFolders.Count > 0)
        {
            Config.Profiles.Clear();
            RebuiltConfigFromDisk = true;
            changed = true;
        }

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

        return changed;
    }

    private static bool IsProfileFolder(string folder)
    {
        return new Profile { Folder = folder }.InstanceNumber > 0;
    }

    private int GetNextAvailableProfileNumber()
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

        return nextNumber;
    }

    private static string TrimNote(string note)
    {
        const int maxLength = 120;
        var trimmed = note.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
