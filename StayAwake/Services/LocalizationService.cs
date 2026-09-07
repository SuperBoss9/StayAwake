using System.Globalization;
using System.Windows;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class LocalizationService
{
    private ResourceDictionary? _current;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.System;

    public event Action? LanguageChanged;

    public void Apply(AppLanguage language)
    {
        CurrentLanguage = language;
        var culture = ResolveCulture(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        var dict = new ResourceDictionary
        {
            Source = new Uri(
                culture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
                    ? "Resources/Strings/Strings.ru.xaml"
                    : "Resources/Strings/Strings.en.xaml",
                UriKind.Relative)
        };

        var app = Application.Current;
        if (app != null)
        {
            if (_current != null)
                app.Resources.MergedDictionaries.Remove(_current);
            app.Resources.MergedDictionaries.Add(dict);
        }

        _current = dict;
        LanguageChanged?.Invoke();
    }

    public static CultureInfo ResolveCulture(AppLanguage language) => language switch
    {
        AppLanguage.English => new CultureInfo("en"),
        AppLanguage.Russian => new CultureInfo("ru"),
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("ru")
            : new CultureInfo("en")
    };

    public string Get(string key, string fallback = "")
    {
        if (_current != null && _current.Contains(key) && _current[key] is string s)
            return s;
        if (Application.Current?.TryFindResource(key) is string found)
            return found;
        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }
}
