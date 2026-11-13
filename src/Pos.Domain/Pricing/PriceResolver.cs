using Pos.Domain.Primitives;

namespace Pos.Domain.Pricing;

public enum PriceSource
{
    PriceList,
    Unit,
    BaseScaled
}

public sealed record ResolvedPrice(long PriceMinor, PriceSource Source, bool IsInherited);

/// <summary>
/// Resolution order: explicit price-list override, the selling unit's own
/// price, then the base price scaled by the unit factor.
/// </summary>
public static class PriceResolver
{
    public static ResolvedPrice Resolve(
        long? priceListPriceMinor,
        long? unitPriceMinor,
        long basePriceMinor,
        long factorToBaseMilli)
    {
        if (basePriceMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePriceMinor));
        }

        if (factorToBaseMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factorToBaseMilli));
        }

        if (priceListPriceMinor is not null)
        {
            return new ResolvedPrice(priceListPriceMinor.Value, PriceSource.PriceList, IsInherited: false);
        }

        if (unitPriceMinor is not null)
        {
            return new ResolvedPrice(unitPriceMinor.Value, PriceSource.Unit, IsInherited: true);
        }

        var scaled = MoneyMath.RoundDiv(checked(basePriceMinor * factorToBaseMilli), 1000);
        return new ResolvedPrice(scaled, PriceSource.BaseScaled, IsInherited: true);
    }
}
