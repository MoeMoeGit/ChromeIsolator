using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ChromeIsolator.Models;
using ChromeIsolator.Services;

// A child invocation acts as a browser process without reading real browser data.
if (args.Any(arg => arg.StartsWith("--user-data-dir=")))
{
    await Task.Delay(500);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

try
{
    var legacyAppearance = JsonSerializer.Deserialize<AppConfig>("""{"Profiles":[]}""")!;
    Check(legacyAppearance.Appearance == "system", "Legacy config defaults to system appearance");
    var darkAppearance = new AppConfig { Appearance = "dark" };
    Check(JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(darkAppearance))!.Appearance == "dark", "Appearance survives config round trip");
    AppPaths.EnsureDirectories();
    var store = new ConfigStore();
    var firstManager = new ProfileManager(store);
    var fresh = store.Load();
    Check(new ProfileManager(store).Config.Profiles.All(p => p.EnableCollectorDebug), "First-run modes survive restart");
    Check(fresh.Profiles.Count == 3 && fresh.Profiles.All(p => p.EnableCollectorDebug && !p.EnableEnvironmentVariation), "First-run collector defaults");
    Check(Profile.NewEnvironment("p7").EnableCollectorDebug, "Added profile collector default");
    var legacy = JsonSerializer.Deserialize<Profile>("""{"Folder":"p7"}""")!;
    Check(!legacy.EnableCollectorDebug, "Legacy missing field remains disabled");
    var disabled = Profile.NewEnvironment("p7");
    disabled.EnableCollectorDebug = false;
    Check(!JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(disabled))!.EnableCollectorDebug, "Explicit false survives round trip");
    foreach (var folder in new[] { "x7", "p-1", "p+1", "p0", "p 1", "../p1", "" })
        Check(new Profile { Folder = folder }.InstanceNumber == 0, $"Reject invalid folder {folder}");

    foreach (var dir in Directory.GetDirectories(AppPaths.ProfilesDir)) Directory.Delete(dir);
    store.Save(new AppConfig { Profiles = [new() { Folder = "p7", DisplayName = "Keep", Note = "Note" }] });
    var manager = new ProfileManager(store);
    Check(manager.Config.Profiles.Single().DisplayName == "Keep" && Directory.Exists(AppPaths.ProfileDir("p7")), "Missing directory retains metadata and is recreated");
    store.Save(new AppConfig { Profiles = [new() { Folder = "p8" }] });
    File.WriteAllText(AppPaths.ConfigFile, "{broken");
    Check(store.Load().Profiles.Single().Folder == "p7" && store.RecoveredFromBackup, "Corrupt primary uses backup");
    File.WriteAllText(AppPaths.ConfigFile, "null");
    Check(store.Load().Profiles.Single().Folder == "p7" && store.RecoveredFromBackup, "Null root uses backup");
    File.WriteAllText(AppPaths.ConfigFile, """{"Profiles":null}""");
    Check(store.Load().Profiles.Single().Folder == "p7" && store.RecoveredFromBackup, "Null profiles uses backup");
    File.WriteAllText(AppPaths.ConfigFile, "broken");
    File.WriteAllText(AppPaths.ConfigBackupFile, "broken");
    manager = new ProfileManager(store);
    Check(manager.Config.Profiles.Count == 1 && manager.Config.Profiles[0].Folder == "p7" && !manager.Config.Profiles[0].EnableCollectorDebug, "Disk recovery preserves legacy mode defaults");
    Check(manager.AddProfile().EnableCollectorDebug, "Manager adds collector-enabled profile");
    File.Delete(AppPaths.ConfigFile);
    Directory.CreateDirectory(AppPaths.ConfigFile);
    var saveErrorRaised = false;
    manager.SaveFailed += _ => saveErrorRaised = true;
    manager.Save();
    Check(saveErrorRaised && manager.LastSaveError is not null && manager.Config.Profiles.Count > 0, "Save failure retains state and reports error");
    var recycled = false;
    var deletionManager = new ProfileManager(store, _ => recycled = true);
    var retained = deletionManager.Config.Profiles.First();
    Check(!deletionManager.MoveProfileToRecycleBin(retained) && !recycled &&
        deletionManager.Config.Profiles.Contains(retained), "Deletion save failure never recycles or removes profile");
    Directory.Delete(AppPaths.ConfigFile);
    deletionManager.Save();
    var failureManager = new ProfileManager(store, _ => throw new IOException("Simulated recycle failure"));
    var beforeDelete = failureManager.Config.Profiles.First();
    try { failureManager.MoveProfileToRecycleBin(beforeDelete); throw new Exception("Recycle should fail"); }
    catch (IOException) { }
    Check(new ProfileManager(store).Config.Profiles.Any(p => p.Folder == beforeDelete.Folder), "Recycle failure rolls back persisted metadata");
    var successManager = new ProfileManager(store, path => Directory.Move(path, path + "-trash"));
    Check(successManager.MoveProfileToRecycleBin(successManager.Config.Profiles.First(p => p.Folder == beforeDelete.Folder)) &&
        !new ProfileManager(store).Config.Profiles.Any(p => p.Folder == beforeDelete.Folder), "Successful deletion survives restart");
    var appHost = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location,
        OperatingSystem.IsWindows() ? ".exe" : null);
    var processManager = new ChromeManager(() => new ChromeInfo(BrowserEngineKind.Chrome, appHost, "test", "1.0"));
    var exitNotifications = 0;
    processManager.ProfileExited += _ => Interlocked.Increment(ref exitNotifications);
    var processProfile = new Profile { Folder = "p99" };
    processManager.Start(processProfile);
    var stop = processManager.StopAsync(processProfile);
    var duplicateStop = processManager.StopAsync(processProfile);
    Check(ReferenceEquals(stop, duplicateStop) && processManager.IsRunning(processProfile), "Stopping process stays tracked and duplicate stop shares completion");
    try { processManager.Start(processProfile); throw new Exception("Restart during stop accepted"); }
    catch (InvalidOperationException) { Console.WriteLine("PASS: Restart rejected during stop"); }
    await stop.WaitAsync(TimeSpan.FromSeconds(10));
    Check(!processManager.IsRunning(processProfile), "Stop completion clears tracked process");
    Check(exitNotifications == 1, "Explicit stop notifies mode settings once");
    processManager.Start(processProfile);
    await processManager.StopAsync(processProfile).WaitAsync(TimeSpan.FromSeconds(10));
    Check(!processManager.IsRunning(processProfile), "Restart after completed stop works");
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var occupied = ((IPEndPoint)listener.LocalEndpoint).Port;
    Check(PortAllocator.FindAvailablePort(occupied) != occupied, "Occupied port skipped");
    var available = PortAllocator.FindAvailablePort(41001);
    Check(PortAllocator.FindAvailablePort(available, reserved: [available]) != available, "Reserved startup port skipped");
    try { PortAllocator.FindAvailablePort(65536); throw new Exception("Invalid port accepted"); }
    catch (InvalidOperationException) { Console.WriteLine("PASS: Invalid port fails safely"); }
}
finally { Directory.Delete(AppPaths.SupportDir, recursive: true); }

namespace ChromeIsolator.Services
{
    // Compile production logic against isolated paths; never access real browser data.
    public static class L10n
    {
        public static string GetString(string key) => key;
        public static string Format(string key, params object[] args) => key;
    }

    public static class AppPaths
    {
        public static string SupportDir { get; } = Path.Combine(Path.GetTempPath(), "ChromeIsolator-tests-" + Guid.NewGuid());
        public static string ProfilesDir => Path.Combine(SupportDir, "Profiles");
        public static string ChromeDir => Path.Combine(SupportDir, "Chrome");
        public static string ConfigFile => Path.Combine(SupportDir, "config.json");
        public static string ConfigBackupFile => ConfigFile + ".bak";
        public static string ProfileDir(string folder) => Path.Combine(ProfilesDir, folder);
        public static void EnsureDirectories() => Directory.CreateDirectory(ProfilesDir);
    }
}
