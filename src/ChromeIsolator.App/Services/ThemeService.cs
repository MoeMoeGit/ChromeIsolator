using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SystemColors = System.Windows.SystemColors;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator.Services;

/// <summary>Application-local colors; native window chrome and system dialogs remain native.</summary>
public static class ThemeService
{
    private static string _mode = "system";
    public static void Initialize(string? mode)
    {
        Apply(mode);
        SystemEvents.UserPreferenceChanged += PreferencesChanged;
        SystemParameters.StaticPropertyChanged += ParametersChanged;
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        SystemParameters.StaticPropertyChanged -= ParametersChanged;
    }

    private static void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => Refresh();
    private static void ParametersChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
    private static void Refresh() => WpfApplication.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(_mode)));

    public static void Apply(string? mode)
    {
        _mode = mode is "light" or "dark" ? mode : "system";
        var dark = _mode == "dark" || (_mode == "system" && ReadPreference("AppsUseLightTheme", 1) == 0);
        var resources = WpfApplication.Current.Resources;
        var light = new ResourceDictionary { Source = new Uri("Themes/Colors.xaml", UriKind.Relative) };
        foreach (var key in light.Keys) resources[key] = light[key];
        if (dark)
        {
            Set("WindowBackgroundBrush", "#191D24"); Set("CardBackgroundBrush", "#252B34");
            Set("ToolbarBackgroundBrush", "#E62B323D"); Set("BorderBrush", "#505D6E");
            Set("BorderLightBrush", "#394351"); Set("TextPrimaryBrush", "#F3F5F8");
            Set("TextSecondaryBrush", "#BBC6D6"); Set("TextTertiaryBrush", "#ABB8CA");
            Set("TextPlaceholderBrush", "#ABB8CA"); Set("AccentBrush", "#3472CF");
            Set("AccentHoverBrush", "#316AC1"); Set("AccentLightBrush", "#334B8CEA");
            Set("AccentLightHoverBrush", "#4D4B8CEA"); Set("StatusRunningBrush", "#75D9A2");
            Set("StatusStartingBrush", "#F3C56D"); Set("StatusStoppedBrush", "#ABB8CA");
            Set("ErrorBrush", "#FFADA6"); Set("ErrorBackgroundBrush", "#26FFADA6");
            Set("HoverOverlayBrush", "#14FFFFFF");
        }
        // No backdrop capture or blur: low graphics tiers and transparency-off use solid surfaces.
        if (ReadPreference("EnableTransparency", 1) == 0 || RenderCapability.Tier < 0x20000)
            resources["ToolbarBackgroundBrush"] = resources["CardBackgroundBrush"];
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "WindowBackgroundBrush", "CardBackgroundBrush", "ToolbarBackgroundBrush" })
                resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "TextPrimaryBrush", "TextSecondaryBrush", "TextTertiaryBrush", "TextPlaceholderBrush", "BorderBrush", "BorderLightBrush", "StatusRunningBrush", "StatusStartingBrush", "StatusStoppedBrush", "ErrorBrush" })
                resources[key] = SystemColors.WindowTextBrush;
            resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
            resources["AccentBrush"] = SystemColors.HighlightBrush;
            resources["AccentHoverBrush"] = SystemColors.HighlightBrush;
        }
        void Set(string key, string color) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static int ReadPreference(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue(name) is int value ? value : fallback;
        }
        catch (System.Security.SecurityException) { return fallback; }
        catch (UnauthorizedAccessException) { return fallback; }
        catch (IOException) { return fallback; }
    }
}
