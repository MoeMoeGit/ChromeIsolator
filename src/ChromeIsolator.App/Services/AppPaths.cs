namespace ChromeIsolator.Services;

public static class AppPaths
{
    public static string SupportDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChromeIsolator");

    public static string ConfigFile => Path.Combine(SupportDir, "config.json");
    public static string ConfigBackupFile => Path.Combine(SupportDir, "config.json.bak");
    public static string ProfilesDir => Path.Combine(SupportDir, "Profiles");
    public static string ChromeDir => Path.Combine(SupportDir, "Chrome");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(SupportDir);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(ChromeDir);
    }

    public static string ProfileDir(string folder) => Path.Combine(ProfilesDir, folder);
}
