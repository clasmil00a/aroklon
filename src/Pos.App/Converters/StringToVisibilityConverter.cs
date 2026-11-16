using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>Converts a non-empty string to Visible and null/empty to Collapsed.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
