using System;
using System.Globalization;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>
/// Maps a stored value to a display label. The parameter is a list of
/// "value=label" pairs separated by semicolons, for example
/// "in=Masuk;out=Keluar". Unknown values are returned unchanged.
/// </summary>
public sealed class StringMapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value?.ToString() ?? string.Empty;
        if (key.Length == 0 || parameter is not string map)
        {
            return key;
        }

        foreach (var pair in map.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (string.Equals(pair[..separator], key, StringComparison.OrdinalIgnoreCase))
            {
                return pair[(separator + 1)..];
            }
        }

        return key;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
