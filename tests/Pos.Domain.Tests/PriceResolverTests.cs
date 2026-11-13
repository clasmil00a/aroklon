using Pos.Domain.Pricing;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class PriceResolverTests
{
    [Fact]
    public void Explicit_price_list_wins()
    {
        var result = PriceResolver.Resolve(5700, 6000, 1600, 6000);
        Assert.Equal(5700, result.PriceMinor);
        Assert.Equal(PriceSource.PriceList, result.Source);
        Assert.False(result.IsInherited);
    }

    [Fact]
    public void Unit_price_is_used_and_marked_inherited()
    {
        var result = PriceResolver.Resolve(null, 6000, 1600, 6000);
        Assert.Equal(6000, result.PriceMinor);
        Assert.Equal(PriceSource.Unit, result.Source);
        Assert.True(result.IsInherited);
    }

    [Fact]
    public void Base_price_scales_by_factor_when_unit_price_is_missing()
    {
        var result = PriceResolver.Resolve(null, null, 1600, 100000);
        Assert.Equal(160000, result.PriceMinor);
        Assert.Equal(PriceSource.BaseScaled, result.Source);
    }

    [Fact]
    public void Base_scaling_rounds_half_up()
    {
        var result = PriceResolver.Resolve(null, null, 10, 250);
        Assert.Equal(3, result.PriceMinor);
    }

    [Fact]
    public void Beras_250g_explicit_price_is_used()
    {
        var result = PriceResolver.Resolve(3000, 3000, 10000, 250);
        Assert.Equal(3000, result.PriceMinor);
        Assert.Equal(PriceSource.PriceList, result.Source);
    }
}
