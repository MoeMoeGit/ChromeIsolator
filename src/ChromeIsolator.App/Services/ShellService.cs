using System.Diagnostics;
using System.Windows;
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
}
