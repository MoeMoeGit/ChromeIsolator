using System.Text.Json;
using ChromeIsolator.Models;

namespace ChromeIsolator.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppConfig Load()
    {
        AppPaths.EnsureDirectories();

        if (TryLoad(AppPaths.ConfigFile, out var config))
        {
            return config;
        }

        if (TryLoad(AppPaths.ConfigBackupFile, out config))
        {
            return config;
        }

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
}
