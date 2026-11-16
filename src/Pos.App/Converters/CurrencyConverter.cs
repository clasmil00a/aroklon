using System;
using System.Globalization;
using System.Windows.Data;

namespace Pos.App.Converters;

/// <summary>
/// Formats integer minor units as Indonesian rupiah, for example
/// 9000 becomes "Rp 9.000". The optional parameter is the number of
/// currency minor digits (0 for IDR).
/// </summary>
public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        decimal amount;
        try
        {
            amount = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or OverflowException)
        {
            return string.Empty;
        }

        var minorUnit = 0;
        if (parameter is string text && int.TryParse(text, out var parsed))
        {
            minorUnit = Math.Clamp(parsed, 0, 4);
        }

        decimal divisor = 1;
        for (var i = 0; i < minorUnit; i++)
        {
            divisor *= 10;
        }

        var scaled = amount / divisor;
        var digits = minorUnit == 0 ? "N0" : "N" + minorUnit;
        var number = Math.Abs(scaled).ToString(
            digits,
            CultureInfo.GetCultureInfo("id-ID"));
        return scaled < 0 ? "-Rp " + number : "Rp " + number;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
