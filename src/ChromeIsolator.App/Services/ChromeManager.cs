using System.Diagnostics;
using ChromeIsolator.Models;

namespace ChromeIsolator.Services;

public sealed class ChromeManager
{
    private readonly Dictionary<string, Process> _processes = [];
    private readonly Dictionary<string, int> _debugPorts = [];

    public event Action<string>? ProfileExited;

    public bool IsRunning(Profile profile)
    {
        return _processes.TryGetValue(profile.Folder, out var process) && !process.HasExited;
    }

    public int? DebugPort(Profile profile)
    {
        return _debugPorts.TryGetValue(profile.Folder, out var port) ? port : null;
    }

    public void Start(Profile profile)
    {
        if (IsRunning(profile))
        {
            return;
        }

        var chromePath = FindChromeExecutable()
            ?? throw new InvalidOperationException("找不到官方 Chrome 可执行文件。后续版本会提供自动下载和准备。");

        var profileDir = AppPaths.ProfileDir(profile.Folder);
        Directory.CreateDirectory(profileDir);

        var port = PortAllocator.FindAvailablePort(40000 + Math.Max(profile.InstanceNumber, 1));
        var startInfo = new ProcessStartInfo
        {
            FileName = chromePath,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add($"--user-data-dir={profileDir}");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--test-type");
        startInfo.ArgumentList.Add($"--remote-debugging-port={port}");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) =>
        {
            _processes.Remove(profile.Folder);
            _debugPorts.Remove(profile.Folder);
            ProfileExited?.Invoke(profile.Folder);
        };

        process.Start();
        _processes[profile.Folder] = process;
        _debugPorts[profile.Folder] = port;
    }

    public void Stop(Profile profile)
    {
        if (!_processes.TryGetValue(profile.Folder, out var process))
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            _processes.Remove(profile.Folder);
            _debugPorts.Remove(profile.Folder);
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

    private static string? FindChromeExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
