using System.Globalization;

namespace Pos.Domain.Primitives;

/// <summary>
/// Quantity arithmetic. Quantities are thousandths of the selling or base unit.
/// A conversion factor is also thousandths: 1 renteng = 6 bungkus = 6000.
/// </summary>
public static class UnitMath
{
    public static long ToBase(long qtyMilli, long factorToBaseMilli)
    {
        if (qtyMilli < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyMilli), "Jumlah tidak boleh negatif.");
        }

        if (factorToBaseMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factorToBaseMilli), "Faktor satuan harus lebih dari nol.");
        }

        return MoneyMath.RoundDiv(checked(qtyMilli * factorToBaseMilli), 1000);
    }

    public static long FromBase(long baseQtyMilli, long factorToBaseMilli)
    {
        if (baseQtyMilli < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseQtyMilli), "Jumlah tidak boleh negatif.");
        }

        if (factorToBaseMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factorToBaseMilli), "Faktor satuan harus lebih dari nol.");
        }

        return MoneyMath.RoundDiv(checked(baseQtyMilli * 1000), factorToBaseMilli);
    }

    public static string FormatQuantity(long qtyMilli, string? unitName = null, string cultureName = "id-ID")
    {
        var value = qtyMilli / 1000m;
        var text = value.ToString("0.###", CultureInfo.GetCultureInfo(cultureName));
        return string.IsNullOrWhiteSpace(unitName) ? text : text + " " + unitName;
    }

    public static bool TryParseQuantity(string? text, out long qtyMilli, string cultureName = "id-ID")
    {
        qtyMilli = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(
                cleaned,
                NumberStyles.Number,
                CultureInfo.GetCultureInfo(cultureName),
                out var value) ||
            value < 0)
        {
            return false;
        }

        qtyMilli = (long)decimal.Round(value * 1000m, 0, MidpointRounding.AwayFromZero);
        return true;
    }
}
