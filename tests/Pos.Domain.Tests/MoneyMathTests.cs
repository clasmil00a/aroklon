using Pos.Domain.Primitives;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class MoneyMathTests
{
    [Theory]
    [InlineData(500, 1000, 1)]
    [InlineData(499, 1000, 0)]
    [InlineData(1500, 1000, 2)]
    [InlineData(-500, 1000, -1)]
    [InlineData(-499, 1000, 0)]
    public void RoundDiv_rounds_half_away_from_zero(long numerator, long denominator, long expected)
    {
        Assert.Equal(expected, MoneyMath.RoundDiv(numerator, denominator));
    }

    [Theory]
    [InlineData(2000, 6000, 12000)]
    [InlineData(1000, 95000, 95000)]
    [InlineData(1, 1600, 2)]
    [InlineData(250, 3000, 750)]
    public void LineTotal_uses_milli_quantities(long qtyMilli, long unitPriceMinor, long expected)
    {
        Assert.Equal(expected, MoneyMath.LineTotal(qtyMilli, unitPriceMinor));
    }

    [Fact]
    public void PercentOf_uses_basis_points()
    {
        Assert.Equal(9500, MoneyMath.PercentOf(95000, 1000));
        Assert.Equal(3000, MoneyMath.PercentOf(100000, 300));
    }

    [Fact]
    public void Format_uses_rupiah_and_indonesian_grouping()
    {
        Assert.Equal("Rp104.000", MoneyMath.Format(104000));
        Assert.Equal("-Rp3.000", MoneyMath.Format(-3000));
        Assert.Equal("Rp1.234,56", MoneyMath.Format(123456, 2));
    }
}
