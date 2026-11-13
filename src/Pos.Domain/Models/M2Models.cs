using Pos.Domain.Pos;
using Pos.Domain.Primitives;

namespace Pos.Domain.Models;

/// <summary>A reusable unit of measure (pcs, bungkus, renteng, dus, kg, g, ...).</summary>
public sealed record UnitOption(string Id, string Name, string? Symbol);

/// <summary>Request to create or rename a reusable unit.</summary>
public sealed record UnitCreateRequest(string Name, string? Symbol);

/// <summary>Request to update a reusable unit.</summary>
public sealed record UnitUpdateRequest(string Name, string? Symbol, bool Active);

/// <summary>One sellable variant of a product: unit + conversion factor + price.</summary>
public sealed record ProductUnitVariant(
    string ProductUnitId,
    string ProductId,
    string UnitId,
    string UnitName,
    string? UnitSymbol,
    long FactorToBaseMilli,
    bool IsBase,
    bool IsDefaultSale,
    long? PriceMinor,
    int SortOrder,
    bool Active,
    long StockBaseMilli)
{
    public long StockInSellingUnitMilli =>
        FactorToBaseMilli <= 0
            ? 0
            : UnitMath.FromBase(StockBaseMilli, FactorToBaseMilli);
}

/// <summary>Request to add a selling variant ("isi") to a product.</summary>
public sealed record ProductUnitCreateRequest(
    string ProductId,
    string UnitId,
    long FactorToBaseMilli,
    bool IsDefaultSale,
    long? PriceMinor,
    int SortOrder);

/// <summary>Request to update a selling variant.</summary>
public sealed record ProductUnitUpdateRequest(
    long? FactorToBaseMilli,
    bool? IsDefaultSale,
    long? PriceMinor,
    int? SortOrder,
    bool? Active);

/// <summary>Full product detail with all selling variants.</summary>
public sealed record ProductDetail(
    string ProductId,
    string Name,
    string? Code,
    string BaseUnitId,
    string BaseUnitName,
    long PriceMinor,
    long CostMinor,
    bool IsService,
    bool PriceChangeAllowed,
    string? Barcode,
    string? GroupName,
    IReadOnlyList<ProductUnitVariant> Variants)
{
    public ProductUnitVariant? BaseVariant => Variants.FirstOrDefault(v => v.IsBase);
    public ProductUnitVariant? DefaultSaleVariant => Variants.FirstOrDefault(v => v.IsDefaultSale) ?? BaseVariant;
}

/// <summary>One price-list item for a product unit, with inheritance marker.</summary>
public sealed record PriceListItemView(
    string ProductUnitId,
    string ProductId,
    string ProductName,
    string UnitName,
    string? UnitSymbol,
    long FactorToBaseMilli,
    long? PriceMinor,
    bool IsInherited,
    int? MarkupBp);

/// <summary>Request to create a price list.</summary>
public sealed record PriceListCreateRequest(string Name, bool ApplyPromotions);

/// <summary>Request to update a price list item price.</summary>
public sealed record PriceListItemUpdateRequest(long? PriceMinor, int? MarkupBp);

/// <summary>Price list summary for lists and dropdowns.</summary>
public sealed record PriceListSummary(string Id, string Name, bool ApplyPromotions, bool Active);

/// <summary>Customer summary for selection and assignment.</summary>
public sealed record CustomerSummary(
    string Id,
    string? Code,
    string Name,
    string Type,
    string? PriceListId,
    string? PriceListName,
    string? DiscountKind,
    long DiscountValue,
    bool Active);

/// <summary>Request to update customer price list / discount.</summary>
public sealed record CustomerUpdateRequest(
    string? PriceListId,
    string? DiscountKind,
    long? DiscountValue,
    bool? Active);

/// <summary>Register summary with default price list.</summary>
public sealed record RegisterSummary(
    string Id,
    string Name,
    string? DefaultPriceListId,
    string? DefaultPriceListName,
    bool Active);

/// <summary>Request to update register default price list.</summary>
public sealed record RegisterUpdateRequest(string? DefaultPriceListId);
