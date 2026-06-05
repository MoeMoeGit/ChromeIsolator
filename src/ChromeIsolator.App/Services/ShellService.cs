using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using WpfClipboard = System.Windows.Clipboard;

namespace ChromeIsolator.Services;

public static class ShellService
{
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public static void CopyText(string text)
    {
        WpfClipboard.SetText(text);
    }

    public static void RequestDefaultBrowser()
    {
        RegisterBrowserCapabilities();
        OpenUrl("ms-settings:defaultapps");
    }

    private static void RegisterBrowserCapabilities()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        using var registeredApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registeredApps?.SetValue("ChromeIsolator", @"Software\Clients\StartMenuInternet\ChromeIsolator\Capabilities");

        using var client = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\ChromeIsolator");
        client?.SetValue("", "ChromeIsolator");

        using var clientDefaultIcon = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\ChromeIsolator\DefaultIcon");
        clientDefaultIcon?.SetValue("", $"\"{executablePath}\",0");

        using var clientCommand = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\ChromeIsolator\shell\open\command");
        clientCommand?.SetValue("", $"\"{executablePath}\"");

        using var capabilities = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\ChromeIsolator\Capabilities");
        capabilities?.SetValue("ApplicationName", "ChromeIsolator");
        capabilities?.SetValue("ApplicationDescription", "Open links in a selected ChromeIsolator environment.");

        using var urlAssociations = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\ChromeIsolator\Capabilities\URLAssociations");
        urlAssociations?.SetValue("http", "ChromeIsolatorURL");
        urlAssociations?.SetValue("https", "ChromeIsolatorURL");

        using var progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChromeIsolatorURL");
        progId?.SetValue("", "ChromeIsolator URL");
        progId?.SetValue("URL Protocol", "");

        using var icon = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChromeIsolatorURL\DefaultIcon");
        icon?.SetValue("", $"\"{executablePath}\",0");

        using var command = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChromeIsolatorURL\shell\open\command");
        command?.SetValue("", $"\"{executablePath}\" \"%1\"");
    }
}
