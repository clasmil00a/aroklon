using System;
using System.Globalization;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>
/// Turns a value into a Boolean by comparing it with the ConverterParameter.
/// Used by radio buttons that select one of several string values.
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}
