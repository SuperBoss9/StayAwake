using System.Windows;
using Microsoft.Win32;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class ThemeService
{
    private ResourceDictionary? _current;

    public ThemeMode CurrentTheme { get; private set; } = ThemeMode.System;

    public event Action? ThemeChanged;

    public void Apply(ThemeMode theme)
    {
        CurrentTheme = theme;
        bool dark = theme switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => IsSystemDark()
        };

        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        var app = Application.Current;
        if (app != null)
        {
            if (_current != null)
                app.Resources.MergedDictionaries.Remove(_current);
            app.Resources.MergedDictionaries.Insert(0, dict);
        }

        _current = dict;
        ThemeChanged?.Invoke();
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0;
        }
        catch
        {
            // ignore
        }
        return false;
    }
}
