using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ChromeIsolator.Models;
using Microsoft.Win32;

namespace ChromeIsolator.Services;

public sealed class ChromeManager
{
    private readonly Func<bool> _isEdgeFallbackAllowed;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Process> _processes = [];
    private readonly Dictionary<string, int> _debugPorts = [];
    private readonly Dictionary<string, FingerprintInjector> _fingerprintInjectors = [];
    private static readonly HttpClient CdpHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public event Action<string>? ProfileExited;

    public ChromeManager(Func<bool>? isEdgeFallbackAllowed = null)
    {
        _isEdgeFallbackAllowed = isEdgeFallbackAllowed ?? (() => false);
    }

    public ChromeInfo? CurrentChrome => ResolveBrowser(_isEdgeFallbackAllowed());
    public ChromeInfo? InstalledChrome => ResolveChrome();
    public ChromeInfo? InstalledEdge => ResolveEdge();

    public bool IsRunning(Profile profile)
    {
        lock (_syncRoot)
        {
            return _processes.TryGetValue(profile.Folder, out var process) && !process.HasExited;
        }
    }

    public int? DebugPort(Profile profile)
    {
        lock (_syncRoot)
        {
            return _debugPorts.TryGetValue(profile.Folder, out var port) ? port : null;
        }
    }

    public void BringToFront(Profile profile)
    {
        Process? process;
        lock (_syncRoot)
        {
            _processes.TryGetValue(profile.Folder, out process);
        }

        if (process is null || process.HasExited) return;

        var handle = process.MainWindowHandle;
        if (handle == IntPtr.Zero) return;

        ShowWindow(handle, SW_RESTORE);
        SetForegroundWindow(handle);
    }

    public void Start(Profile profile)
    {
        var chromePath = CurrentChrome?.ExecutablePath
            ?? throw new InvalidOperationException(L10n.GetString("ChromeNotFound"));

        var profileDir = AppPaths.ProfileDir(profile.Folder);
        Directory.CreateDirectory(profileDir);

        lock (_syncRoot)
        {
            if (_processes.TryGetValue(profile.Folder, out var existing))
            {
                if (!existing.HasExited)
                {
                    return;
                }

                _processes.Remove(profile.Folder);
                _debugPorts.Remove(profile.Folder);
                if (_fingerprintInjectors.Remove(profile.Folder, out var staleInjector))
                {
                    _ = staleInjector.DisposeAsync();
                }

                existing.Dispose();
            }

            var port = PortAllocator.FindAvailablePort(40000 + Math.Max(profile.InstanceNumber, 1));
            var runningCount = _processes.Count;
            var offsetX = 50 + runningCount * 30;
            var offsetY = 50 + runningCount * 30;
            var startInfo = new ProcessStartInfo
            {
                FileName = chromePath,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add($"--user-data-dir={profileDir}");
            startInfo.ArgumentList.Add("--no-first-run");
            startInfo.ArgumentList.Add($"--remote-debugging-port={port}");
            startInfo.ArgumentList.Add($"--window-position={offsetX},{offsetY}");

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.Exited += (_, _) =>
            {
                FingerprintInjector? injector;
                Process? exitedProcess;
                lock (_syncRoot)
                {
                    _fingerprintInjectors.Remove(profile.Folder, out injector);
                    _processes.Remove(profile.Folder, out exitedProcess);
                    _debugPorts.Remove(profile.Folder);
                }

                if (injector is not null)
                {
                    _ = injector.DisposeAsync();
                }

                exitedProcess?.Dispose();

                ProfileExited?.Invoke(profile.Folder);
            };

            try
            {
                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException(L10n.GetString("ChromeNotFound"));
                }
            }
            catch
            {
                process.Dispose();
                throw;
            }

            var injector = new FingerprintInjector(port, profile.InstanceNumber);
            _processes[profile.Folder] = process;
            _debugPorts[profile.Folder] = port;
            _fingerprintInjectors[profile.Folder] = injector;

            _ = injector.StartAsync();
        }
    }

    public void Stop(Profile profile)
    {
        Process? process;
        FingerprintInjector? injector;
        int? port = null;
        lock (_syncRoot)
        {
            _processes.Remove(profile.Folder, out process);
            if (_debugPorts.Remove(profile.Folder, out var debugPort))
            {
                port = debugPort;
            }
            _fingerprintInjectors.Remove(profile.Folder, out injector);
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (injector is not null)
            {
                injector.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            }

            if (!process.HasExited)
            {
                if (port is not null)
                {
                    TryCloseBrowserViaCdpAsync(port.Value).Wait(TimeSpan.FromSeconds(2));
                }

                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }

                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public void StopAll(IEnumerable<Profile> profiles)
    {
        foreach (var profile in profiles.ToList())
        {
            Stop(profile);
        }
    }

    public async Task PrepareChromeAsync(IProgress<(double Percent, string Status)>? progress = null, CancellationToken cancellationToken = default)
    {
        if (ResolveChrome() is not null)
        {
            progress?.Report((100, L10n.GetString("ChromeReady")));
            return;
        }

        Directory.CreateDirectory(AppPaths.ChromeDir);
        var installerPath = Path.Combine(AppPaths.ChromeDir, "GoogleChromeStandaloneEnterprise64.msi");
        var downloadUrl = new Uri("https://dl.google.com/chrome/install/GoogleChromeStandaloneEnterprise64.msi");

        progress?.Report((0, L10n.GetString("ChromeConnecting")));

        using (var httpClient = new HttpClient())
        using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;

            progress?.Report((0, L10n.GetString("ChromeDownloading")));

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(installerPath);
            var buffer = new byte[128 * 1024];
            long readBytes = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readBytes += read;
                if (totalBytes is > 0)
                {
                    var percent = (double)readBytes / totalBytes.Value * 80;
                    var downloadedMB = readBytes / (1024.0 * 1024.0);
                    var totalMB = totalBytes.Value / (1024.0 * 1024.0);
                    progress?.Report((percent, L10n.Format("ChromeDownloadingProgress", $"{downloadedMB:F1}", $"{totalMB:F1}")));
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((80, L10n.GetString("ChromeInstalling")));

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"/i \"{installerPath}\" /passive /norestart"
        });
        if (process is not null)
        {
            await process.WaitForExitAsync();
        }

        progress?.Report((90, L10n.GetString("ChromeVerifying")));

        if (ResolveChrome() is null)
        {
            throw new InvalidOperationException(L10n.GetString("ChromeInstallFailed"));
        }

        progress?.Report((100, L10n.GetString("ChromeDone")));
    }

    private static ChromeInfo? ResolveBrowser(bool allowEdgeFallback)
    {
        return ResolveChrome() ?? (allowEdgeFallback ? ResolveEdge() : null);
    }

    private static ChromeInfo? ResolveChrome()
    {
        var candidates = new[]
        {
            (ReadChromePathFromRegistry(Registry.LocalMachine), "系统 Chrome"),
            (ReadChromePathFromRegistry(Registry.CurrentUser), "用户 Chrome"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"), "系统 Chrome"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"), "系统 Chrome"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"), "用户 Chrome")
        };

        foreach (var (path, source) in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return new ChromeInfo(BrowserEngineKind.Chrome, path, source, FileVersionInfo.GetVersionInfo(path).ProductVersion);
            }
        }

        return null;
    }

    private static ChromeInfo? ResolveEdge()
    {
        var candidates = new[]
        {
            (ReadEdgePathFromRegistry(Registry.LocalMachine), "备用 Edge"),
            (ReadEdgePathFromRegistry(Registry.CurrentUser), "备用 Edge"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"), "备用 Edge"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"), "备用 Edge"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"), "备用 Edge")
        };

        foreach (var (path, source) in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return new ChromeInfo(BrowserEngineKind.Edge, path, source, FileVersionInfo.GetVersionInfo(path).ProductVersion);
            }
        }

        return null;
    }

    private static string? ReadChromePathFromRegistry(RegistryKey root)
    {
        using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe");
        return key?.GetValue("") as string;
    }

    private static string? ReadEdgePathFromRegistry(RegistryKey root)
    {
        using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
        return key?.GetValue("") as string;
    }

    private static async Task TryCloseBrowserViaCdpAsync(int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await CdpHttpClient.GetAsync($"http://127.0.0.1:{port}/json/version", cts.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
            if (!json.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsUrl) ||
                !Uri.TryCreate(wsUrl.GetString(), UriKind.Absolute, out var uri))
            {
                return;
            }

            using var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(uri, cts.Token).ConfigureAwait(false);
            var payload = Encoding.UTF8.GetBytes("""{"id":1,"method":"Browser.close"}""");
            await webSocket.SendAsync(payload, WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Fall back to the normal window close / process kill path.
        }
    }
}
