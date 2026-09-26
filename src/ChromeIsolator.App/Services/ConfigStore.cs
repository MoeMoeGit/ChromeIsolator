using System.Text.Json;
using ChromeIsolator.Models;

namespace ChromeIsolator.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public bool LoadedExistingConfig { get; private set; }
    public bool RecoveredFromBackup { get; private set; }
    public bool UsedDefaultAfterLoadFailure { get; private set; }

    public AppConfig Load()
    {
        AppPaths.EnsureDirectories();
        LoadedExistingConfig = false;
        RecoveredFromBackup = false;
        UsedDefaultAfterLoadFailure = false;

        if (TryLoad(AppPaths.ConfigFile, out var config))
        {
            LoadedExistingConfig = true;
            return config;
        }

        var primaryExists = File.Exists(AppPaths.ConfigFile);
        var backupExists = File.Exists(AppPaths.ConfigBackupFile);
        if (primaryExists)
        {
            try
            {
                File.Copy(AppPaths.ConfigFile, Path.Combine(AppPaths.SupportDir,
                    $"config.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"));
            }
            catch { /* Recovery may still succeed when a diagnostic copy cannot be written. */ }
        }
        if (TryLoad(AppPaths.ConfigBackupFile, out config))
        {
            RecoveredFromBackup = true;
            TryRestorePrimaryFromBackup();
            LoadedExistingConfig = true;
            return config;
        }

        UsedDefaultAfterLoadFailure = primaryExists || backupExists;
        return AppConfig.CreateNew();
    }

    public void Save(AppConfig config)
    {
        AppPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempFile = Path.Combine(AppPaths.SupportDir, $"config-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempFile, json);

            if (File.Exists(AppPaths.ConfigFile))
            {
                File.Replace(tempFile, AppPaths.ConfigFile, AppPaths.ConfigBackupFile, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFile, AppPaths.ConfigFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static bool TryLoad(string path, out AppConfig config)
    {
        try
        {
            if (!File.Exists(path))
            {
                config = new AppConfig();
                return false;
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (loaded?.Profiles is null || loaded.Profiles.Any(profile => profile is null))
            {
                config = new AppConfig();
                return false;
            }
            loaded.Profiles = loaded.Profiles.Where(profile => profile.InstanceNumber > 0)
                .GroupBy(profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToList();
            config = loaded;
            return true;
        }
        catch
        {
            config = new AppConfig();
            return false;
        }
    }

    private static void TryRestorePrimaryFromBackup()
    {
        try
        {
            File.Copy(AppPaths.ConfigBackupFile, AppPaths.ConfigFile, overwrite: true);
        }
        catch
        {
            // Loading from backup is enough to keep the app usable; restore can be retried next launch.
        }
    }
}
