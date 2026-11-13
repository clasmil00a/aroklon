using Pos.Domain.Primitives;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class UnitMathTests
{
    [Theory]
    [InlineData(2000, 6000, 12000)]
    [InlineData(1000, 100000, 100000)]
    [InlineData(1000, 250, 250)]
    [InlineData(1000, 1, 1)]
    public void ToBase_converts_selling_quantity_to_base_milli(
        long qtyMilli,
        long factorToBaseMilli,
        long expected)
    {
        Assert.Equal(expected, UnitMath.ToBase(qtyMilli, factorToBaseMilli));
    }

    [Theory]
    [InlineData(12000, 6000, 2000)]
    [InlineData(100000, 100000, 1000)]
    public void FromBase_is_the_inverse(long baseQtyMilli, long factor, long expected)
    {
        Assert.Equal(expected, UnitMath.FromBase(baseQtyMilli, factor));
    }

    [Theory]
    [InlineData(250, "0,25")]
    [InlineData(1500, "1,5")]
    [InlineData(1000, "1")]
    [InlineData(12000, "12")]
    public void FormatQuantity_keeps_up_to_three_decimals(long qtyMilli, string expected)
    {
        Assert.Equal(expected, UnitMath.FormatQuantity(qtyMilli));
    }

    [Theory]
    [InlineData("0,25", 250)]
    [InlineData("1,5", 1500)]
    [InlineData("2", 2000)]
    [InlineData("12,345", 12345)]
    [InlineData("1.000", 1000000)]
    public void TryParseQuantity_accepts_indonesian_decimal_and_grouping(string text, long expected)
    {
        Assert.True(UnitMath.TryParseQuantity(text, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("bukan angka")]
    [InlineData("")]
    public void TryParseQuantity_rejects_invalid_input(string text)
    {
        Assert.False(UnitMath.TryParseQuantity(text, out _));
    }
}
