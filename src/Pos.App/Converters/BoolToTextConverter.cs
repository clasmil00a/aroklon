using System;
using System.Globalization;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>
/// Maps a Boolean to one of two labels. The parameter is written as
/// "true text|false text"; the false part may be left empty.
/// </summary>
public sealed class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var texts = (parameter as string ?? string.Empty).Split('|');
        var whenTrue = texts.Length > 0 ? texts[0] : string.Empty;
        var whenFalse = texts.Length > 1 ? texts[1] : string.Empty;
        return value is bool flag && flag ? whenTrue : whenFalse;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
