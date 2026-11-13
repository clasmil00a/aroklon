using System.Globalization;

namespace Pos.Domain.Primitives;

/// <summary>
/// Integer money arithmetic. Money is minor units; IDR has minor unit 0.
/// Rounding is half away from zero at the point of calculation.
/// </summary>
public static class MoneyMath
{
    public const long QuantityScale = 1000;

    public static long RoundDiv(long numerator, long denominator)
    {
        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "Pembagi harus lebih dari nol.");
        }

        return numerator >= 0
            ? (numerator + denominator / 2) / denominator
            : -((-numerator + denominator / 2) / denominator);
    }

    /// <summary>qty_milli × unit_price_minor / 1000, rounded half up.</summary>
    public static long LineTotal(long qtyMilli, long unitPriceMinor)
    {
        if (qtyMilli < 0 || unitPriceMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyMilli), "Jumlah dan harga tidak boleh negatif.");
        }

        return RoundDiv(checked(qtyMilli * unitPriceMinor), QuantityScale);
    }

    /// <summary>Percentage in basis points: 1100 = 11.00%.</summary>
    public static long PercentOf(long amountMinor, int rateBp)
    {
        if (amountMinor < 0 || rateBp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Nilai tidak boleh negatif.");
        }

        return RoundDiv(checked(amountMinor * rateBp), 10_000);
    }

    public static string Format(long amountMinor, int currencyMinorUnit = 0, string cultureName = "id-ID")
    {
        if (currencyMinorUnit is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(currencyMinorUnit));
        }

        decimal divisor = 1;
        for (var i = 0; i < currencyMinorUnit; i++)
        {
            divisor *= 10;
        }

        var value = amountMinor / divisor;
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var digits = currencyMinorUnit == 0 ? "N0" : "N" + currencyMinorUnit;
        var text = Math.Abs(value).ToString(digits, culture);
        return (value < 0 ? "-Rp" : "Rp") + text;
    }
}
