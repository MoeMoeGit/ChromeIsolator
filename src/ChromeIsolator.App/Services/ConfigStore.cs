using System.Text.Json;
using ChromeIsolator.Models;

namespace ChromeIsolator.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public bool RecoveredFromBackup { get; private set; }
    public bool UsedDefaultAfterLoadFailure { get; private set; }

    public AppConfig Load()
    {
        AppPaths.EnsureDirectories();
        RecoveredFromBackup = false;
        UsedDefaultAfterLoadFailure = false;

        if (TryLoad(AppPaths.ConfigFile, out var config))
        {
            return config;
        }

        var primaryExists = File.Exists(AppPaths.ConfigFile);
        var backupExists = File.Exists(AppPaths.ConfigBackupFile);
        if (TryLoad(AppPaths.ConfigBackupFile, out config))
        {
            RecoveredFromBackup = true;
            TryRestorePrimaryFromBackup();
            return config;
        }

        UsedDefaultAfterLoadFailure = primaryExists || backupExists;
        return new AppConfig();
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
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
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
