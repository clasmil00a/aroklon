using Pos.Domain.Pos;
using Pos.Domain.Primitives;

namespace Pos.Domain.Models;

/// <summary>A sellable product resolved to one selling unit.</summary>
public sealed record ProductLookup(
    string ProductId,
    string Name,
    string? Code,
    string ProductUnitId,
    string BaseUnitId,
    string UnitName,
    long UnitFactorMilli,
    long PriceMinor,
    long CostMinor,
    long StockBaseMilli,
    bool IsService,
    bool PriceChangeAllowed,
    string? Barcode,
    string? GroupName)
{
    public long StockInSellingUnitMilli =>
        UnitFactorMilli <= 0
            ? 0
            : UnitMath.FromBase(StockBaseMilli, UnitFactorMilli);
}

/// <summary>Tax rate applicable to products.</summary>
public sealed record TaxRateSummary(string Id, string Name, int RateBp, bool IsDefault);

public sealed record ProductCreateRequest(
    string Name,
    string? Code,
    string BaseUnitId,
    long PriceMinor,
    long CostMinor,
    long OpeningStockBaseMilli,
    string? Barcode,
    bool IsService,
    bool PriceChangeAllowed = true,
    string? GroupName = null);

public sealed record SaleLineInput(
    string ProductId,
    string ProductUnitId,
    long QtyMilli,
    long? UnitPriceOverrideMinor,
    DiscountKind DiscountKind,
    long DiscountValue,
    string? TaxRateId = null,
    string? Comment = null);

public sealed record ParkSaleRequest(
    IReadOnlyList<SaleLineInput> Lines,
    string? CustomerId,
    string? Note,
    string? ClientRequestId = null);

public sealed record CheckoutRequest(
    IReadOnlyList<SaleLineInput> Lines,
    string PaymentTypeId,
    long TenderedMinor,
    string? CustomerId = null,
    string? Note = null,
    string? ClientRequestId = null,
    string? ParkedSaleId = null,
    DiscountKind CartDiscountKind = DiscountKind.None,
    long CartDiscountValue = 0);

public sealed record SaleResult(
    string SaleId,
    string Number,
    string Status,
    long SubtotalMinor,
    long DiscountMinor,
    long TaxMinor,
    long TotalMinor,
    long PaidMinor,
    long ChangeMinor,
    bool IsExisting);

public sealed record SaleSummary(
    string Id,
    string Number,
    string Status,
    string Type,
    string CreatedAt,
    string? CustomerName,
    string UserName,
    int ItemCount,
    long TotalMinor,
    long PaidMinor);

public sealed record OpenSaleSummary(
    string Id,
    string Number,
    string CreatedAt,
    string? CustomerName,
    string UserName,
    int ItemCount,
    long TotalMinor,
    string? Note);

public sealed record SaleLineDetail(
    string Id,
    string? ProductId,
    string? ProductUnitId,
    string Name,
    string? Code,
    string UnitName,
    long UnitFactorMilli,
    long QtyMilli,
    long BaseQtyMilli,
    long UnitPriceMinor,
    long DiscountMinor,
    long TotalMinor,
    string Status,
    string? Comment);

public sealed record SalePaymentDetail(
    string Id,
    string PaymentTypeName,
    long AmountMinor,
    long TenderedMinor,
    long ChangeMinor,
    string CreatedAt);

public sealed record SaleDetail(
    string Id,
    string Number,
    string Status,
    string Type,
    string CreatedAt,
    string? CustomerName,
    string UserName,
    string RegisterName,
    long SubtotalMinor,
    long DiscountMinor,
    long TaxMinor,
    long TotalMinor,
    long PaidMinor,
    long ChangeMinor,
    long BalanceMinor,
    string? Note,
    IReadOnlyList<SaleLineDetail> Lines,
    IReadOnlyList<SalePaymentDetail> Payments)
{
    public int ItemCount => Lines.Count;

    public static IReadOnlyList<SaleLineDetail> EmptyLines { get; } = [];
}

public sealed record SaleFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Query = null,
    int Limit = 200);

/// <summary>Cash movement (kas masuk/keluar) request.</summary>
public sealed record CashMovementRequest(
    string RegisterId,
    string Direction, // 'in' or 'out'
    long AmountMinor,
    string Reason);

/// <summary>Cash movement result.</summary>
public sealed record CashMovementResult(
    string Id,
    string Direction,
    long AmountMinor,
    long BalanceMinor,
    string CreatedAt);

/// <summary>Cash movement summary for listing.</summary>
public sealed record CashMovementSummary(
    string Id,
    string Direction,
    long AmountMinor,
    string Reason,
    string CreatedAt,
    string UserName);

/// <summary>Refund line input.</summary>
public sealed record RefundLineInput(
    string SaleItemId,
    long RefundQtyMilli,
    string? Reason);

/// <summary>Refund request.</summary>
public sealed record RefundRequest(
    string SaleId,
    IReadOnlyList<RefundLineInput> Lines,
    string PaymentTypeId,
    long TenderedMinor,
    string Reason);

/// <summary>Refund result.</summary>
public sealed record RefundResult(
    string RefundSaleId,
    string RefundNumber,
    string Status,
    long RefundTotalMinor,
    long PaidMinor,
    long ChangeMinor);

/// <summary>Refund summary for listing.</summary>
public sealed record RefundSummary(
    string Id,
    string Number,
    string CreatedAt,
    string OriginalSaleNumber,
    long RefundTotalMinor,
    string UserName);

/// <summary>End of day request.</summary>
public sealed record EndOfDayRequest(
    string RegisterId,
    long ClosingCashMinor,
    string? VarianceNote);

/// <summary>End of day result.</summary>
public sealed record EndOfDayResult(
    string ReportNumber,
    string BusinessDate,
    long SalesCount,
    long RefundCount,
    long TaxableTotalMinor,
    long TaxTotalMinor,
    long DiscountTotalMinor,
    long GrandTotalMinor,
    long CashInTotalMinor,
    long CashOutTotalMinor,
    long ClosingCashMinor,
    long ExpectedCashMinor,
    long VarianceMinor)
{
    public long CashIn => CashInTotalMinor;
    public long CashOut => CashOutTotalMinor;
    public long GrandTotal => GrandTotalMinor;
    public long ExpectedCash => ExpectedCashMinor;
}
