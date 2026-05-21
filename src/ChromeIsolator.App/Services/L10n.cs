using System.Globalization;
using System.Windows;

namespace ChromeIsolator.Services;

public static class L10n
{
    private static readonly Dictionary<string, string> LanguageFiles = new()
    {
        ["zh"] = "Resources/Strings.xaml",
        ["en"] = "Resources/Strings.en.xaml",
        ["ja"] = "Resources/Strings.ja.xaml",
        ["ko"] = "Resources/Strings.ko.xaml",
        ["de"] = "Resources/Strings.de.xaml",
        ["fr"] = "Resources/Strings.fr.xaml",
        ["ru"] = "Resources/Strings.ru.xaml"
    };

    private static readonly Dictionary<string, string> NativeNames = new()
    {
        ["zh"] = "中文",
        ["en"] = "English",
        ["ja"] = "日本語",
        ["ko"] = "한국어",
        ["de"] = "Deutsch",
        ["fr"] = "Français",
        ["ru"] = "Русский"
    };

    public static event Action? LanguageChanged;
    public static string CurrentLanguage { get; private set; } = "zh";

    public static IReadOnlyList<(string Code, string NativeName)> SupportedLanguages =>
        LanguageFiles.Keys.Select(k => (k, NativeNames[k])).ToList();

    public static void Initialize(string? savedLanguage)
    {
        if (!string.IsNullOrWhiteSpace(savedLanguage) && LanguageFiles.ContainsKey(savedLanguage))
        {
            SetLanguage(savedLanguage);
            return;
        }

        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SetLanguage(LanguageFiles.ContainsKey(culture) ? culture : "zh");
    }

    public static void SetLanguage(string language)
    {
        if (!LanguageFiles.ContainsKey(language))
        {
            return;
        }

        CurrentLanguage = language;

        var app = System.Windows.Application.Current;
        if (app is null) return;

        var dictUri = new Uri($"pack://application:,,,/{LanguageFiles[language]}", UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = dictUri };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Strings") == true);
        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(newDict);
        LanguageChanged?.Invoke();
    }

    public static string GetString(string key)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return key;

        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Contains(key))
            {
                return dict[key]?.ToString() ?? key;
            }
        }

        return key;
    }

    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}
