using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Services;
using Pos.Data.Tests;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Xunit;

namespace Pos.Data.Tests;

public sealed class CashMovementTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public Task InitializeAsync() => SetUpAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();
    }

    [Fact]
    public async Task Cash_in_then_out_updates_balance()
    {
        var inResult = await _db.CashMovements.CreateAsync(new(
            "register-1", "in", 100_000, "Setoran awal"));

        Assert.Equal("in", inResult.Direction);
        Assert.Equal(100_000, inResult.AmountMinor);
        Assert.Equal(100_000, inResult.BalanceMinor);

        var outResult = await _db.CashMovements.CreateAsync(new(
            "register-1", "out", 30_000, "Beli sembako"));

        Assert.Equal("out", outResult.Direction);
        Assert.Equal(30_000, outResult.AmountMinor);
        Assert.Equal(70_000, outResult.BalanceMinor);

        var movements = await _db.CashMovements.ListAsync();
        Assert.Equal(2, movements.Count);
    }

    [Fact]
    public async Task Cash_out_exceeding_balance_is_rejected()
    {
        await _db.CashMovements.CreateAsync(new("register-1", "in", 50_000, "Awal"));

        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.CashMovements.CreateAsync(new("register-1", "out", 100_000, "Terlalu besar")));
    }

    [Fact]
    public async Task Invalid_direction_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.CashMovements.CreateAsync(new("register-1", "sideways", 10_000, "Salah")));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_cash()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.CashMovements.CreateAsync(new("register-1", "in", 10_000, "Test")));
    }
}

public sealed class RefundTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private string _saleId = null!;
    private string _saleItemId = null!;
    private string _productId = null!;
    private string _unitId = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();

        await using var connection = await _db.OpenAsync();
        var now = Pos.Data.Services.SqlText.Now(_db.Clock.UtcNow);
        _productId = Pos.Domain.Ids.NewId();
        _unitId = Pos.Domain.Ids.NewId();
        _saleId = Pos.Domain.Ids.NewId();
        _saleItemId = Pos.Domain.Ids.NewId();
        var adminId = await connection.ExecuteScalarAsync<string>(
            "SELECT id FROM users WHERE username = 'admin';");

        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO units (id, name, symbol, active) VALUES ('unit-pcs', 'pcs', 'pcs', 1);");
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO tax_rates (id, name, rate_bp, active, is_default) VALUES ('tax-refund-test', 'PPN 11% Refund Test', 1100, 1, 0);");
        await connection.ExecuteAsync(
            """
            INSERT INTO products (id, name, code, active, default_qty_milli, cost_minor, price_minor, price_change_allowed, created_at, updated_at)
            VALUES (@id, 'Beras Refund', 'REF-001', 1, 1000, 8000, 12000, 1, @now, @now);
            """,
            new { id = _productId, now });
        await connection.ExecuteAsync(
            """
            INSERT INTO product_units (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale, price_minor, sort_order, active, created_at, updated_at)
            VALUES (@unitId, @productId, 'unit-pcs', 1000, 1, 1, 12000, 0, 1, @now, @now);
            """,
            new { unitId = _unitId, productId = _productId, now });
        await connection.ExecuteAsync(
            "INSERT INTO product_taxes (product_id, tax_rate_id) VALUES (@productId, 'tax-refund-test');",
            new { productId = _productId });
        await connection.ExecuteAsync(
            """
            INSERT INTO stock_movements (id, product_id, type, qty_delta_base_milli, unit_cost_minor, reference_type, reference_id, note, user_id, created_at)
            VALUES ('sm-refund-1', @productId, 'opening', 10_000, 8000, 'product', @productId, 'Stok awal', @adminId, @now);
            INSERT INTO stock_movements (id, product_id, type, qty_delta_base_milli, unit_cost_minor, reference_type, reference_id, note, user_id, created_at)
            VALUES ('sm-refund-2', @productId, 'sale', -10_000, 8000, 'sale', @saleId, 'Beras Refund', @adminId, @now);
            """,
            new { productId = _productId, adminId, now, saleId = _saleId });
        await connection.ExecuteAsync(
            """
            INSERT INTO business_days (id, register_id, business_date, status, opened_at, opened_by, opening_cash_minor, created_at, updated_at)
            VALUES ('bd-refund', 'register-1', strftime('%Y-%m-%d','now'), 'open', @now, @adminId, 0, @now, @now);
            """,
            new { adminId, now });
        await connection.ExecuteAsync(
            """
            INSERT INTO sales (id, number, type, status, register_id, business_day_id, user_id, customer_id, price_list_id, currency, currency_minor_unit, subtotal_minor, discount_kind, discount_value, discount_total_minor, tax_total_minor, total_minor, paid_total_minor, change_minor, balance_minor, created_at, updated_at, closed_at)
            VALUES (@saleId, 'REFUND-TEST-001', 'sale', 'closed', 'register-1', 'bd-refund', @adminId, NULL, 'pl-retail', 'IDR', 0, 120_000, 'none', 0, 0, 13_200, 133_200, 133_200, 0, 0, @now, @now, @now);
            """,
            new { saleId = _saleId, adminId, now });
        await connection.ExecuteAsync(
            """
            INSERT INTO sale_items (id, sale_id, product_id, product_unit_id, name_snapshot, unit_name_snapshot, unit_factor_milli, qty_milli, base_qty_milli, unit_price_minor, unit_cost_minor, discount_kind, discount_value, discount_total_minor, tax_total_minor, line_total_minor, status, sort_order, created_at, updated_at)
            VALUES (@saleItemId, @saleId, @productId, @unitId, 'Beras Refund', 'pcs', 1000, 10_000, 10_000, 12000, 8000, 'none', 0, 0, 13_200, 133_200, 'confirmed', 0, @now, @now);
            """,
            new { saleItemId = _saleItemId, saleId = _saleId, productId = _productId, unitId = _unitId, now });
        await connection.ExecuteAsync(
            """
            INSERT INTO payments (id, sale_id, payment_type_id, kind, amount_minor, tendered_minor, change_minor, status, created_by, created_at, updated_at)
            VALUES ('pay-refund-1', @saleId, 'pmt-cash', 'tender', 133_200, 150_000, 16_800, 'captured', @adminId, @now, @now);
            """,
            new { saleId = _saleId, adminId, now });
    }

    [Fact]
    public async Task Full_refund_reverses_sale_and_stock()
    {
        var result = await _db.Refunds.CreateAsync(new(
            _saleId,
            [new RefundLineInput(_saleItemId, 10_000, "Pelanggan batal")],
            "pmt-cash",
            133_200,
            "Pelanggan batal"));

        Assert.Equal("closed", result.Status);
        Assert.Equal(133_200, result.RefundTotalMinor);
        Assert.Equal(133_200, result.PaidMinor);

        // Original sale should be marked as refunded.
        await using var connection = await _db.OpenAsync();
        var status = await connection.ExecuteScalarAsync<string>(
            "SELECT status FROM sales WHERE id = @id;", new { id = _saleId });
        Assert.Equal("refunded", status);

        // Stock should be restored (10_000 base milli added back).
        var stock = await connection.ExecuteScalarAsync<long>(
            "SELECT COALESCE(SUM(qty_delta_base_milli), 0) FROM stock_movements WHERE product_id = @id;",
            new { id = _productId });
        // Opening 10_000 - sale 10_000 + refund 10_000 = 10_000
        Assert.Equal(10_000, stock);
    }

    [Fact]
    public async Task Partial_refund_marks_partially_refunded()
    {
        var result = await _db.Refunds.CreateAsync(new(
            _saleId,
            [new RefundLineInput(_saleItemId, 5_000, "Sebagian")],
            "pmt-cash",
            66_600,
            "Sebagian"));

        Assert.Equal("closed", result.Status);
        Assert.Equal(66_600, result.RefundTotalMinor); // Half of 133_200

        await using var connection = await _db.OpenAsync();
        var status = await connection.ExecuteScalarAsync<string>(
            "SELECT status FROM sales WHERE id = @id;", new { id = _saleId });
        Assert.Equal("partially_refunded", status);
    }

    [Fact]
    public async Task Refund_exceeding_refundable_qty_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.Refunds.CreateAsync(new(
                _saleId,
                [new RefundLineInput(_saleItemId, 20_000, "Terlalu banyak")],
                "pmt-cash",
                200_000,
                "Test")));
    }

    [Fact]
    public async Task Refund_list_returns_refunded_sales()
    {
        await _db.Refunds.CreateAsync(new(
            _saleId,
            [new RefundLineInput(_saleItemId, 10_000, "Test")],
            "pmt-cash",
            133_200,
            "Test"));

        var refunds = await _db.Refunds.ListAsync();
        Assert.Single(refunds);
        Assert.Equal("REFUND-TEST-001", refunds[0].OriginalSaleNumber);
    }

    [Fact]
    public async Task Cashier_can_refund()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        var result = await _db.Refunds.CreateAsync(new(
            _saleId,
            [new RefundLineInput(_saleItemId, 5_000, "Test")],
            "pmt-cash",
            66_600,
            "Test"));

        Assert.Equal("closed", result.Status);
        Assert.Equal(66_600, result.RefundTotalMinor);
    }
}

public sealed class EndOfDayTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();
    }

    [Fact]
    public async Task Close_day_creates_z_report_and_variance()
    {
        // First, create some cash movement to have opening + sales.
        await _db.CashMovements.CreateAsync(new("register-1", "in", 500_000, "Setoran awal hari"));

        var result = await _db.EndOfDay.CloseAsync(new(
            "register-1", 550_000, "Penyesuaian"));

        Assert.NotNull(result.ReportNumber);
        Assert.StartsWith("Z-", result.ReportNumber);
        Assert.Equal(500_000, result.ExpectedCashMinor);
        Assert.Equal(550_000, result.ClosingCashMinor);
        Assert.Equal(50_000, result.VarianceMinor);

        // Closed day should appear in list.
        var closed = await _db.EndOfDay.ListClosedDaysAsync();
        Assert.Single(closed);
        Assert.Equal(result.ReportNumber, closed[0].ReportNumber);
    }

    [Fact]
    public async Task Close_day_without_open_day_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.NotFoundException>(() =>
            _db.EndOfDay.CloseAsync(new("register-1", 100_000, null)));
    }

    [Fact]
    public async Task Non_admin_cannot_close_day()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.EndOfDay.CloseAsync(new("register-1", 100_000, null)));
    }
}
