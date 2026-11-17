using System;
using System.Globalization;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>
/// Converts a milli-unit conversion factor to readable "isi" text,
/// e.g. 6000 (1 renteng = 6 bungkus) becomes "6".
/// </summary>
public sealed class MilliToQuantityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long milli)
        {
            return (milli / 1000m).ToString("0.###", CultureInfo.GetCultureInfo("id-ID"));
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
