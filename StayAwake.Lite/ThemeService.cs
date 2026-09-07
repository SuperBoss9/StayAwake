using System.Windows;
using Microsoft.Win32;

namespace StayAwake.Lite;

internal static class ThemeService
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ResourceDictionary? _current;

    public static void Initialize()
    {
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(ApplySystemTheme);
        }
    }

    public static void ApplySystemTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        bool light = IsAppsLightTheme();
        var uri = new Uri(
            light ? "Themes/Light.xaml" : "Themes/Dark.xaml",
            UriKind.Relative);

        var dict = new ResourceDictionary { Source = uri };
        var merged = app.Resources.MergedDictionaries;

        if (_current != null)
            merged.Remove(_current);

        merged.Insert(0, dict);
        _current = dict;
    }

    public static bool IsAppsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i != 0;
        }
        catch
        {
            // fall through
        }

        return true;
    }
}
