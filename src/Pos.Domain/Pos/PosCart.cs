using Pos.Domain.Models;
using Pos.Domain.Primitives;

namespace Pos.Domain.Pos;

public enum DiscountKind
{
    None,
    Percent,
    Amount
}

public sealed record Discount(DiscountKind Kind, long Value)
{
    public static Discount None { get; } = new(DiscountKind.None, 0);

    public static string ToDatabase(DiscountKind kind) => kind switch
    {
        DiscountKind.Percent => "percent",
        DiscountKind.Amount => "amount",
        _ => "none"
    };

    public static DiscountKind FromDatabase(string? value) => value switch
    {
        "percent" => DiscountKind.Percent,
        "amount" => DiscountKind.Amount,
        _ => DiscountKind.None
    };
}

public sealed record CartLine(
    string LineId,
    string ProductId,
    string ProductUnitId,
    string Name,
    string? Code,
    string UnitName,
    long UnitFactorMilli,
    long QtyMilli,
    long UnitPriceMinor,
    long UnitCostMinor,
    DiscountKind DiscountKind,
    long DiscountValue,
    string? Comment,
    bool PriceOverridden)
{
    public long BaseQtyMilli => UnitMath.ToBase(QtyMilli, UnitFactorMilli);

    public long GrossMinor => MoneyMath.LineTotal(QtyMilli, UnitPriceMinor);

    public long DiscountMinor => CartMath.DiscountFor(GrossMinor, DiscountKind, DiscountValue);

    public long TotalMinor => GrossMinor - DiscountMinor;
}

public sealed record CartTotals(
    long SubtotalMinor,
    long DiscountMinor,
    long TaxMinor,
    long TotalMinor,
    long QuantityMilli,
    int LineCount)
{
    public static CartTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

public static class CartMath
{
    public const long MaxPercentBasisPoints = 10_000;

    public static long DiscountFor(long grossMinor, DiscountKind kind, long value)
    {
        if (grossMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grossMinor));
        }

        return kind switch
        {
            DiscountKind.Percent when value is >= 0 and <= MaxPercentBasisPoints =>
                MoneyMath.PercentOf(grossMinor, (int)value),
            DiscountKind.Amount when value >= 0 => Math.Min(value, grossMinor),
            DiscountKind.None when value == 0 => 0,
            DiscountKind.Percent => throw new ArgumentOutOfRangeException(
                nameof(value), "Diskon persen harus antara 0 dan 10000 basis poin."),
            DiscountKind.Amount => throw new ArgumentOutOfRangeException(
                nameof(value), "Diskon nominal tidak boleh negatif."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value), "Diskon tidak boleh diisi untuk jenis none.")
        };
    }

    public static CartTotals Calculate(IEnumerable<CartLine> lines, Discount? cartDiscount = null)
    {
        var cartDisc = cartDiscount ?? Discount.None;
        var subtotal = 0L;
        var lineDiscount = 0L;
        var quantity = 0L;
        var count = 0;

        foreach (var line in lines)
        {
            if (line.QtyMilli <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lines), "Jumlah baris harus lebih dari nol.");
            }

            subtotal = checked(subtotal + line.GrossMinor);
            lineDiscount = checked(lineDiscount + line.DiscountMinor);
            quantity = checked(quantity + line.BaseQtyMilli);
            count++;
        }

        var cartDiscountMinor = DiscountFor(subtotal - lineDiscount, cartDisc.Kind, cartDisc.Value);
        var totalDiscount = checked(lineDiscount + cartDiscountMinor);

        return new CartTotals(subtotal, totalDiscount, 0, checked(subtotal - totalDiscount), quantity, count);
    }
}

/// <summary>
/// In-memory cart used by the cashier screen. Checkout never trusts these
/// prices: the sale service resolves and validates them again against the
/// database. The cart only exists for fast, local feedback.
/// </summary>
public sealed class PosCart
{
    private readonly List<CartLine> _lines = [];
    private Discount _cartDiscount = Discount.None;

    public IReadOnlyList<CartLine> Lines => _lines;

    public Discount CartDiscount
    {
        get => _cartDiscount;
        private set => _cartDiscount = value;
    }

    public CartTotals Totals => CartMath.Calculate(_lines, _cartDiscount);

    public bool IsEmpty => _lines.Count == 0;

    public CartLine Add(
        ProductLookup product,
        long qtyMilli,
        long? unitPriceOverrideMinor = null,
        DiscountKind discountKind = DiscountKind.None,
        long discountValue = 0,
        string? comment = null)
    {
        if (qtyMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyMilli), "Jumlah harus lebih dari nol.");
        }

        var price = unitPriceOverrideMinor ?? product.PriceMinor;
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPriceOverrideMinor));
        }

        CartMath.DiscountFor(MoneyMath.LineTotal(qtyMilli, price), discountKind, discountValue);

        var existing = _lines.FirstOrDefault(line =>
            line.ProductId == product.ProductId &&
            line.ProductUnitId == product.ProductUnitId &&
            line.UnitPriceMinor == price &&
            line.DiscountKind == discountKind &&
            line.DiscountValue == discountValue);
        if (existing is not null && string.IsNullOrWhiteSpace(comment))
        {
            var merged = existing with { QtyMilli = checked(existing.QtyMilli + qtyMilli) };
            Replace(existing.LineId, merged);
            return merged;
        }

        var line = new CartLine(
            Ids.NewId(),
            product.ProductId,
            product.ProductUnitId,
            product.Name,
            product.Code,
            product.UnitName,
            product.UnitFactorMilli,
            qtyMilli,
            price,
            product.CostMinor,
            discountKind,
            discountValue,
            comment,
            unitPriceOverrideMinor is not null && unitPriceOverrideMinor != product.PriceMinor);

        _lines.Add(line);
        return line;
    }

    public CartLine? Find(string lineId) => _lines.FirstOrDefault(line => line.LineId == lineId);

    public void SetQuantity(string lineId, long qtyMilli)
    {
        if (qtyMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyMilli), "Jumlah harus lebih dari nol.");
        }

        var line = Require(lineId);
        Replace(lineId, line with { QtyMilli = qtyMilli });
    }

    public void SetUnitPrice(string lineId, long priceMinor)
    {
        if (priceMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceMinor), "Harga tidak boleh negatif.");
        }

        var line = Require(lineId);
        Replace(lineId, line with
        {
            UnitPriceMinor = priceMinor,
            PriceOverridden = true
        });
    }

    public void SetDiscount(string lineId, DiscountKind kind, long value)
    {
        var line = Require(lineId);
        CartMath.DiscountFor(line.GrossMinor, kind, value);
        Replace(lineId, line with { DiscountKind = kind, DiscountValue = value });
    }

    public void SetComment(string lineId, string? comment)
    {
        var line = Require(lineId);
        Replace(lineId, line with { Comment = comment });
    }

    public void Remove(string lineId)
    {
        var line = Require(lineId);
        _lines.Remove(line);
    }

    public void SetCartDiscount(DiscountKind kind, long value)
    {
        CartMath.DiscountFor(0, kind, value); // validate
        _cartDiscount = new Discount(kind, value);
    }

    public void ClearCartDiscount() => _cartDiscount = Discount.None;

    public void ReplaceLine(CartLine line)
    {
        if (line.QtyMilli <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Jumlah baris harus lebih dari nol.");
        }

        Replace(line.LineId, line);
    }

    public void Clear()
    {
        _lines.Clear();
        _cartDiscount = Discount.None;
    }

    public void Load(IEnumerable<CartLine> lines)
    {
        _lines.Clear();
        foreach (var line in lines)
        {
            if (line.QtyMilli <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lines), "Jumlah baris harus lebih dari nol.");
            }

            _lines.Add(line);
        }
    }

    private CartLine Require(string lineId) =>
        Find(lineId) ?? throw new NotFoundException("pos.line_not_found", "Baris transaksi tidak ditemukan.");

    private void Replace(string lineId, CartLine replacement)
    {
        var index = _lines.FindIndex(line => line.LineId == lineId);
        if (index < 0)
        {
            throw new NotFoundException("pos.line_not_found", "Baris transaksi tidak ditemukan.");
        }

        _lines[index] = replacement;
    }
}
