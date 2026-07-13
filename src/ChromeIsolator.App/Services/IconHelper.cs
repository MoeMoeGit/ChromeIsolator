using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ChromeIsolator.Services;

public static class IconHelper
{
    private static BitmapFrame? _cachedIcon;

    public static void ApplyIcon(Window window)
    {
        window.Icon = GetIcon();
    }

    private static BitmapFrame? GetIcon()
    {
        if (_cachedIcon is not null)
        {
            return _cachedIcon;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                // The resource stream is disposed when this method returns.  Load the ICO
                // eagerly so WPF (and the Windows taskbar) never needs that closed stream
                // when it retrieves the window icon later.
                _cachedIcon = BitmapFrame.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                _cachedIcon.Freeze();
                return _cachedIcon;
            }
        }

        return null;
    }
}
