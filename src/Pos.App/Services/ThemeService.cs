using System.Windows;

namespace Pos.App.Services;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Swaps the first merged dictionary. Tokens.xaml and Controls.xaml stay in
/// place so every DynamicResource updates without reloading a view.
/// </summary>
public sealed class ThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        if (theme == Current)
        {
            return;
        }

        var application = Application.Current
            ?? throw new InvalidOperationException("Aplikasi belum siap.");

        var source = theme == AppTheme.Light
            ? new Uri("Themes/Theme.Light.xaml", UriKind.Relative)
            : new Uri("Themes/Theme.Dark.xaml", UriKind.Relative);

        var dictionary = new ResourceDictionary { Source = source };
        var merged = application.Resources.MergedDictionaries;
        if (merged.Count == 0)
        {
            merged.Add(dictionary);
        }
        else
        {
            merged[0] = dictionary;
        }

        Current = theme;
    }

    public static AppTheme Parse(string? value) =>
        string.Equals(value, "light", StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Light
            : AppTheme.Dark;
}
