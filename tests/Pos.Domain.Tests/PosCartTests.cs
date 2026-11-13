using Pos.Domain.Models;
using Pos.Domain.Pos;
using Pos.Domain.Primitives;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class PosCartTests
{
    [Fact]
    public void Empty_cart_has_zero_totals()
    {
        var cart = new PosCart();

        Assert.True(cart.IsEmpty);
        Assert.Equal(0, cart.Totals.TotalMinor);
        Assert.Equal(0, cart.Totals.LineCount);
    }

    [Fact]
    public void Add_line_and_merge_same_product_unit_price()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 15_000);

        var first = cart.Add(product, 1000);
        var second = cart.Add(product, 2000);

        Assert.Equal(first.LineId, second.LineId);
        Assert.Single(cart.Lines);
        Assert.Equal(3000, cart.Lines[0].QtyMilli);
        Assert.Equal(45_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Different_units_or_prices_create_separate_lines()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 15_000);

        cart.Add(product, 1000);
        cart.Add(product, 1000, unitPriceOverrideMinor: 20_000);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(35_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Set_quantity_and_price_update_totals()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 15_000);
        var line = cart.Add(product, 1000);

        cart.SetQuantity(line.LineId, 2500);
        cart.SetUnitPrice(line.LineId, 16_000);

        var updated = cart.Find(line.LineId)!;
        Assert.Equal(2500, updated.QtyMilli);
        Assert.Equal(16_000, updated.UnitPriceMinor);
        Assert.True(updated.PriceOverridden);
        Assert.Equal(40_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Set_discount_calculates_line_total()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 10_000);
        var line = cart.Add(product, 2000);

        cart.SetDiscount(line.LineId, DiscountKind.Percent, 1000); // 10%

        var updated = cart.Find(line.LineId)!;
        Assert.Equal(2_000, updated.DiscountMinor);
        Assert.Equal(18_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Remove_line_empties_cart()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 10_000);
        var line = cart.Add(product, 1000);

        cart.Remove(line.LineId);

        Assert.True(cart.IsEmpty);
        Assert.Null(cart.Find(line.LineId));
    }

    [Fact]
    public void Load_lines_replaces_cart()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 10_000);
        var existing = cart.Add(product, 1000);

        var replacement = existing with { QtyMilli = 5000 };
        cart.ReplaceLine(replacement);

        Assert.Single(cart.Lines);
        Assert.Equal(5000, cart.Lines[0].QtyMilli);
        Assert.Equal(50_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Base_quantity_converts_by_unit_factor()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Dus", "renteng", 6_000, 120_000); // 1 renteng = 6 bungkus
        var line = cart.Add(product, 2_000); // 2 renteng

        Assert.Equal(12_000, line.BaseQtyMilli); // 2 * 6000 / 1000
        Assert.Equal(240_000, cart.Totals.TotalMinor);
    }

    [Fact]
    public void Zero_or_negative_quantity_is_rejected()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 10_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.Add(product, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.Add(product, -100));

        var line = cart.Add(product, 1000);
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.SetQuantity(line.LineId, 0));
    }

    [Fact]
    public void Negative_price_is_rejected()
    {
        var cart = new PosCart();
        var product = Lookup("p-1", "Beras", "pcs", 1000, 10_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.SetUnitPrice(cart.Add(product, 1000).LineId, -1));
    }

    private static ProductLookup Lookup(string id, string name, string unit, long factor, long price) =>
        new(id, name, null, id, "unit-base", unit, factor, price, 0, 0, false, true, null, null);
}

public sealed class CartMathTests
{
    [Theory]
    [InlineData(DiscountKind.None, 0, 0)]
    [InlineData(DiscountKind.Percent, 1000, 2_000)] // 10% of 20_000
    [InlineData(DiscountKind.Percent, 10_000, 20_000)] // 100% -> free
    [InlineData(DiscountKind.Amount, 5_000, 5_000)]
    [InlineData(DiscountKind.Amount, 50_000, 20_000)] // capped at gross
    public void Discount_for(DiscountKind kind, long value, long expected)
    {
        Assert.Equal(expected, CartMath.DiscountFor(20_000, kind, value));
    }

    [Theory]
    [InlineData(DiscountKind.Percent, -1)]
    [InlineData(DiscountKind.Percent, 10_001)]
    [InlineData(DiscountKind.Amount, -1)]
    [InlineData(DiscountKind.None, 1)]
    public void Invalid_discount_is_rejected(DiscountKind kind, long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CartMath.DiscountFor(20_000, kind, value));
    }

    [Fact]
    public void Totals_sum_lines()
    {
        var line1 = new CartLine(
            "l-1", "p-1", "u-1", "Beras", null, "pcs", 1000, 1000, 10_000, 0,
            DiscountKind.None, 0, null, false);
        var line2 = new CartLine(
            "l-2", "p-2", "u-2", "Gula", null, "kg", 1000, 2000, 12_000, 0,
            DiscountKind.Percent, 500, null, false); // 5% of 24_000 = 1_200

        var totals = CartMath.Calculate(new[] { line1, line2 });

        Assert.Equal(34_000, totals.SubtotalMinor);
        Assert.Equal(1_200, totals.DiscountMinor);
        Assert.Equal(32_800, totals.TotalMinor);
        Assert.Equal(3000, totals.QuantityMilli);
        Assert.Equal(2, totals.LineCount);
    }
}
