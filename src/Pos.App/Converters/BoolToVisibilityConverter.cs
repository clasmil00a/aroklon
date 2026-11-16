using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>Converts true to Visible and false to Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            bool flag => flag ? Visibility.Visible : Visibility.Collapsed,
            string text => string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible,
            null => Visibility.Collapsed,
            _ => Visibility.Visible,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
