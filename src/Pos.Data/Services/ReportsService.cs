using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;
using Pos.Domain.Primitives;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Pos.Data.Services;

public sealed class ReportsService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public ReportsService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ReportDefinition>> ListReportsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        var reports = new List<ReportDefinition>
        {
            new ReportDefinition(
                "sales_report",
                "Laporan Penjualan",
                "Laporan penjualan per periode",
                "sales",
                true, true, true, true,
                true, true),
            new ReportDefinition(
                "stock_report",
                "Laporan Stok",
                "Laporan stok barang per produk",
                "stock",
                true, false, false, true,
                true, true),
            new ReportDefinition(
                "payment_report",
                "Laporan Pembayaran",
                "Laporan pembayaran per tipe pembayaran",
                "payments",
                true, false, true, false,
                true, true),
            new ReportDefinition(
                "customer_report",
                "Laporan Pelanggan",
                "Laporan transaksi per pelanggan",
                "customers",
                true, true, false, false,
                true, true),
            new ReportDefinition(
                "tax_report",
                "Laporan Pajak",
                "Laporan pajak per periode",
                "tax",
                true, false, false, false,
                true, true),
        };

        return reports;
    }

    public async Task<string> GenerateReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        var reportType = parameters.ReportId switch
        {
            "sales_report" => await GenerateSalesReportAsync(parameters, cancellationToken),
            "stock_report" => await GenerateStockReportAsync(parameters, cancellationToken),
            "payment_report" => await GeneratePaymentReportAsync(parameters, cancellationToken),
            "customer_report" => await GenerateCustomerReportAsync(parameters, cancellationToken),
            "tax_report" => await GenerateTaxReportAsync(parameters, cancellationToken),
            _ => throw new NotFoundException("report.not_found", "Laporan tidak ditemukan."),
        };

        return reportType;
    }

    /// <summary>
    /// Exports a report as CSV (Excel-compatible) bytes.
    /// </summary>
    public async Task<byte[]> ExportReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        var text = await GenerateReportAsync(parameters, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    private async Task<string> GenerateSalesReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var from = parameters.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "1970-01-01";
        var to = parameters.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "9999-12-31";

        var rows = await connection.QueryAsync<SalesReportRow>(new CommandDefinition(
            """
            SELECT
                s.number AS Number,
                s.created_at AS CreatedAt,
                s.customer_name_snapshot AS CustomerName,
                u.full_name AS UserName,
                COUNT(si.id) AS ItemCount,
                s.total_minor AS TotalMinor,
                s.tax_total_minor AS TaxMinor,
                s.discount_total_minor AS DiscountMinor
            FROM sales s
            JOIN users u ON u.id = s.user_id
            LEFT JOIN sale_items si ON si.sale_id = s.id AND si.status <> 'voided'
            WHERE s.status = 'closed'
              AND s.created_at >= @from
              AND s.created_at <= @to
              AND (@customer_id IS NULL OR s.customer_id = @customer_id)
              AND (@user_id IS NULL OR s.user_id = @user_id)
            GROUP BY s.id
            ORDER BY s.created_at DESC;
            """,
            new
            {
                from = from + " 00:00:00",
                to = to + " 23:59:59",
                customer_id = parameters.CustomerId,
                user_id = parameters.UserId,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var total = rows.Sum(r => r.TotalMinor);
        var totalItems = rows.Sum(r => r.ItemCount);
        var totalStr = MoneyMath.Format(total);

        var lines = new List<string>
        {
            "Laporan Penjualan",
            $"Periode: {parameters.FromDate?.ToString("dd/MM/yyyy") ?? "-"} s.d {parameters.ToDate?.ToString("dd/MM/yyyy") ?? "-"}",
            $"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}",
            "",
            "No. Transaksi | Tanggal | Pelanggan | Kasir | Item | Subtotal | Pajak | Diskon | Total",
            new string('-', 120),
        };

        foreach (var row in rows)
        {
            var lineTotalStr = MoneyMath.Format(row.TotalMinor);
            var taxStr = MoneyMath.Format(row.TaxMinor);
            var discountStr = MoneyMath.Format(row.DiscountMinor);
            lines.Add($"{row.Number,-18} | {row.CreatedAt,19} | {row.CustomerName ?? "-",-15} | {row.UserName,-15} | {row.ItemCount,5} | {lineTotalStr,10} | {taxStr,8} | {discountStr,9} | {totalStr,10}");
        }

        lines.Add(new string('-', 120));
        lines.Add($"Total: {rows.Count()} transaksi | {totalItems} item | {totalStr}");

        return string.Join("\n", lines);
    }

    private async Task<string> GenerateStockReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<StockReportRow>(new CommandDefinition(
            """
            SELECT
                p.name AS ProductName,
                bu.name AS UnitName,
                COALESCE(SUM(sm.qty_delta_base_milli), 0) AS QtyBaseMilli,
                SUM(CASE WHEN sm.type = 'sale' THEN -sm.qty_delta_base_milli ELSE sm.qty_delta_base_milli END) AS NetQty
            FROM products p
            JOIN product_units bpu ON bpu.product_id = p.id AND bpu.is_base = 1
            JOIN units bu ON bu.id = bpu.unit_id
            LEFT JOIN stock_movements sm ON sm.product_id = p.id
            WHERE p.active = 1
            GROUP BY p.id
            HAVING COALESCE(SUM(sm.qty_delta_base_milli), 0) <> 0
            ORDER BY p.name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var lines = new List<string>
        {
            "Laporan Stok",
            $"Tanggal: {DateTime.Now:dd/MM/yyyy HH:mm}",
            "",
            "Produk | Satuan | Stok Akhir",
            new string('-', 60),
        };

        foreach (var row in rows)
        {
            var qtyStr = MoneyMath.Format(row.QtyBaseMilli * 1000);
            lines.Add($"{row.ProductName,-30} | {row.UnitName,-10} | {qtyStr,10}");
        }

        lines.Add(new string('-', 60));
        lines.Add($"Total {rows.Count()} produk");

        return string.Join("\n", lines);
    }

    private async Task<string> GeneratePaymentReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<PaymentReportRow>(new CommandDefinition(
            """
            SELECT
                pt.name AS PaymentType,
                COUNT(p.id) AS Count,
                SUM(p.amount_minor) AS TotalAmount
            FROM payments p
            JOIN payment_types pt ON pt.id = p.payment_type_id
            WHERE p.status = 'captured'
              AND p.created_at >= @from
              AND p.created_at <= @to
            GROUP BY pt.id
            ORDER BY TotalAmount DESC;
            """,
            new
            {
                from = parameters.FromDate?.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture) ?? "1970-01-01",
                to = parameters.ToDate?.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture) ?? "9999-12-31",
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var lines = new List<string>
        {
            "Laporan Pembayaran",
            $"Periode: {parameters.FromDate?.ToString("dd/MM/yyyy") ?? "-"} s.d {parameters.ToDate?.ToString("dd/MM/yyyy") ?? "-"}",
            $"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}",
            "",
            "Tipe Pembayaran | Jumlah | Total",
            new string('-', 60),
        };

        var grandTotal = 0L;
        foreach (var row in rows)
        {
            var amountStr = MoneyMath.Format(row.TotalAmount);
            lines.Add($"{row.PaymentType,-20} | {row.Count,6} | {amountStr,10}");
            grandTotal += row.TotalAmount;
        }

        var grandTotalStr = MoneyMath.Format(grandTotal);
        lines.Add(new string('-', 60));
        lines.Add($"Total: {grandTotalStr}");

        return string.Join("\n", lines);
    }

    private async Task<string> GenerateCustomerReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<CustomerReportRow>(new CommandDefinition(
            """
            SELECT
                c.name AS CustomerName,
                COUNT(s.id) AS OrderCount,
                SUM(s.total_minor) AS TotalSpent
            FROM customers c
            LEFT JOIN sales s ON s.customer_id = c.id AND s.status = 'closed'
              AND s.created_at >= @from
              AND s.created_at <= @to
            GROUP BY c.id
            ORDER BY TotalSpent DESC;
            """,
            new
            {
                from = parameters.FromDate?.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture) ?? "1970-01-01",
                to = parameters.ToDate?.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture) ?? "9999-12-31",
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var lines = new List<string>
        {
            "Laporan Pelanggan",
            $"Periode: {parameters.FromDate?.ToString("dd/MM/yyyy") ?? "-"} s.d {parameters.ToDate?.ToString("dd/MM/yyyy") ?? "-"}",
            $"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}",
            "",
            "Pelanggan | Order | Total",
            new string('-', 60),
        };

        foreach (var row in rows)
        {
            var customerTotalStr = MoneyMath.Format(row.TotalSpent);
            lines.Add($"{row.CustomerName,-30} | {row.OrderCount,6} | {customerTotalStr,10}");
        }

        lines.Add(new string('-', 60));
        lines.Add($"Total: {rows.Count()} pelanggan");

        return string.Join("\n", lines);
    }

    private async Task<string> GenerateTaxReportAsync(
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<TaxReportRow>(new CommandDefinition(
            """
            SELECT
                tr.name AS TaxName,
                tr.rate_bp AS RateBp,
                COALESCE(SUM(si.tax_total_minor), 0) AS TaxAmount,
                SUM(s.total_minor) AS TotalAmount
            FROM sales s
            JOIN sale_items si ON si.sale_id = s.id AND si.status <> 'voided'
            JOIN sale_item_taxes sit ON sit.sale_item_id = si.id
            JOIN tax_rates tr ON tr.id = sit.tax_rate_id
            WHERE s.status = 'closed'
              AND s.created_at >= @from
              AND s.created_at <= @to
            GROUP BY tr.id
            ORDER BY TaxAmount DESC;
            """,
            new
            {
                from = parameters.FromDate?.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture) ?? "1970-01-01",
                to = parameters.ToDate?.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture) ?? "9999-12-31",
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var lines = new List<string>
        {
            "Laporan Pajak",
            $"Periode: {parameters.FromDate?.ToString("dd/MM/yyyy") ?? "-"} s.d {parameters.ToDate?.ToString("dd/MM/yyyy") ?? "-"}",
            $"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}",
            "",
            "Pajak | Tarif | Jumlah Pajak | Total Transaksi",
            new string('-', 70),
        };

        var grandTax = 0L;
        var grandTotal = 0L;
        foreach (var row in rows)
        {
            var taxStr = MoneyMath.Format(row.TaxAmount);
            var totalStr = MoneyMath.Format(row.TotalAmount);
            lines.Add($"{row.TaxName,-20} | {row.RateBp / 100.0:N2}% | {taxStr,10} | {totalStr,10}");
            grandTax += row.TaxAmount;
            grandTotal += row.TotalAmount;
        }

        var grandTaxStr = MoneyMath.Format(grandTax);
        var grandTotalStr = MoneyMath.Format(grandTotal);
        lines.Add(new string('-', 70));
        lines.Add($"Total: {grandTaxStr} | {grandTotalStr}");

        return string.Join("\n", lines);
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class SalesReportRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public long TotalMinor { get; set; }
        public long TaxMinor { get; set; }
        public long DiscountMinor { get; set; }
    }

    private sealed class StockReportRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public long QtyBaseMilli { get; set; }
    }

    private sealed class PaymentReportRow
    {
        public string PaymentType { get; set; } = string.Empty;
        public int Count { get; set; }
        public long TotalAmount { get; set; }
    }

    private sealed class CustomerReportRow
    {
        public string CustomerName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public long TotalSpent { get; set; }
    }

    private sealed class TaxReportRow
    {
        public string TaxName { get; set; } = string.Empty;
        public int RateBp { get; set; }
        public long TaxAmount { get; set; }
        public long TotalAmount { get; set; }
    }
}