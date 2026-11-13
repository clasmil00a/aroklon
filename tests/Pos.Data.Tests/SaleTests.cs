using Dapper;
using Pos.Data.Services;
using Pos.Data.Tests;
using Pos.Domain;
using Pos.Domain.Documents;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Xunit;

namespace Pos.Data.Tests;

public sealed class SaleTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private string _productId = null!;
    private string _unitId = null!;

    public Task InitializeAsync() => SetUpAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();

        await using var connection = await _db.OpenAsync();
        var now = SqlText.Now(_db.Clock.UtcNow);

        _productId = Ids.NewId();
        _unitId = Ids.NewId();

        await connection.ExecuteAsync(
            """
            INSERT INTO products
              (id, name, code, active, default_qty_milli, cost_minor, price_minor,
               price_change_allowed, created_at, updated_at)
            VALUES
              (@id, 'Beras 5kg', 'BER-001', 1, 1000, 8_000, 12_000, 1, @now, @now);
            INSERT INTO product_units
              (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale,
               price_minor, sort_order, active, created_at, updated_at)
            VALUES
              (@unitId, @id, 'unit-pcs', 1000, 1, 1, 12_000, 0, 1, @now, @now);
            INSERT INTO product_barcodes
              (id, product_unit_id, barcode, is_primary, created_at)
            VALUES
              ('bc-1', @unitId, '8991234567890', 1, @now);
            INSERT INTO stock_movements
              (id, product_id, type, qty_delta_base_milli, unit_cost_minor,
               reference_type, reference_id, note, user_id, created_at)
            VALUES
              ('sm-1', @id, 'opening', 10_000, 8_000, 'product', @id, 'Stok awal',
               (SELECT id FROM users WHERE username = 'admin'), @now);
            """,
            new { id = _productId, unitId = _unitId, now });
    }

    [Fact]
    public async Task Park_then_resume_and_pay_creates_one_closed_sale()
    {
        var session = _db.Sessions.Current!;
        var clientRequest = "crc-checkout-1";

        var parked = await _db.Sale.ParkAsync(new ParkSaleRequest(
            [
                new SaleLineInput(_productId, _unitId, 3_000, null,
                    DiscountKind.None, 0)
            ],
            null, "Beras untuk ini", ClientRequestId: clientRequest));

        Assert.Equal("open", parked.Status);
        Assert.Equal(36_000, parked.TotalMinor); // 3 * 12_000

        var openSales = await _db.Sale.ListOpenAsync();
        Assert.Single(openSales);
        Assert.Equal("Beras untuk ini", openSales[0].Note); // note on park

        var completed = await _db.Sale.CheckoutAsync(new CheckoutRequest(
            [
                new SaleLineInput(_productId, _unitId, 3_000, null,
                    DiscountKind.None, 0)
            ],
            "pmt-cash",
            50_000,
            ParkedSaleId: parked.SaleId));

        // Same request id, sale already closed -> existing sale returned.
        var again = await _db.Sale.CheckoutAsync(new CheckoutRequest(
            [
                new SaleLineInput(_productId, _unitId, 3_000, null,
                    DiscountKind.None, 0)
            ],
            "pmt-cash",
            50_000,
            ClientRequestId: clientRequest,
            ParkedSaleId: parked.SaleId));

        Assert.False(completed.IsExisting);
        Assert.True(again.IsExisting);
        Assert.Equal("closed", again.Status);
        Assert.Equal(14_000, again.ChangeMinor); // 50_000 - 36_000

        var openAfter = await _db.Sale.ListOpenAsync();
        Assert.Empty(openAfter);

        var detail = await _db.Sale.GetAsync(completed.SaleId);
        Assert.Equal("closed", detail.Status);
        Assert.Single(detail.Lines);
        Assert.Equal(3_000, detail.Lines[0].QtyMilli);
        Assert.Equal(3_000, detail.Lines[0].BaseQtyMilli);
        Assert.Equal(36_000, detail.TotalMinor);
        Assert.Single(detail.Payments);
        Assert.Equal("Cash", detail.Payments[0].PaymentTypeName);
        Assert.Equal(36_000, detail.Payments[0].AmountMinor);

        // Stock decreased from 10_000 to 7_000 base milli.
        await using var connection = await _db.OpenAsync();
        var stock = await connection.ExecuteScalarAsync<long>(
            "SELECT COALESCE(SUM(qty_delta_base_milli), 0) FROM stock_movements WHERE product_id = @id;",
            new { id = _productId });
        Assert.Equal(7_000, stock);
    }

    [Fact]
    public async Task Receipt_shows_unit_and_totals()
    {
        await _db.Sale.CheckoutAsync(new CheckoutRequest(
            [
                new SaleLineInput(_productId, _unitId, 2_500, null,
                    DiscountKind.Percent, 1000)
            ],
            "pmt-cash",
            30_000));

        var history = await _db.Sale.ListAsync(new SaleFilter { Limit = 10 });
        Assert.Single(history);

        var detail = await _db.Sale.GetAsync(history[0].Id);
        var document = await _db.Sale.GetReceiptAsync(detail.Id);
        var text = SaleService.RenderReceipt(document);

        Assert.Contains(document.Number, text);
        Assert.Contains("Beras 5kg", text);
        Assert.Contains("pcs", text);
        Assert.Contains("Rp", text);
        Assert.Contains("TOTAL", text);
    }

    [Fact]
    public async Task Insufficient_stock_blocks_sale()
    {
        await using var connection = await _db.OpenAsync();
        await connection.ExecuteAsync(
            "DELETE FROM stock_movements WHERE product_id = @id;",
            new { id = _productId });

        await Assert.ThrowsAsync<Pos.Domain.ConflictException>(() =>
            _db.Sale.CheckoutAsync(new CheckoutRequest(
                [
                    new SaleLineInput(_productId, _unitId, 50_000, null,
                        DiscountKind.None, 0)
                ],
                "pmt-cash",
                100_000)));
    }

    [Fact]
    public async Task Park_without_lines_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.Sale.ParkAsync(new ParkSaleRequest([], null, null)));
    }

    [Fact]
    public async Task Void_open_sale_removes_it_from_list()
    {
        var parked = await _db.Sale.ParkAsync(new ParkSaleRequest(
            [new SaleLineInput(_productId, _unitId, 1_000, null,
                DiscountKind.None, 0)],
            null, null, ClientRequestId: "crc-void-1"));

        await _db.Sale.VoidOpenAsync(parked.SaleId, "Pelanggan batal");

        var openSales = await _db.Sale.ListOpenAsync();
        Assert.Empty(openSales);

        await using var connection = await _db.OpenAsync();
        var status = await connection.ExecuteScalarAsync<string>(
            "SELECT status FROM sales WHERE id = @id;", new { id = parked.SaleId });
        Assert.Equal("voided", status);
    }

    [Fact]
    public async Task Barcode_lookup_resolves_selling_unit()
    {
        var product = await _db.Product.FindByBarcodeAsync("8991234567890");
        Assert.NotNull(product);
        Assert.Equal(_productId, product.ProductId);
        Assert.Equal("pcs", product.UnitName);
        Assert.Equal(12_000, product.PriceMinor);
    }

    [Fact]
    public async Task Search_finds_product_by_name_and_barcode()
    {
        var byName = await _db.Product.SearchAsync("Beras", 10);
        Assert.Single(byName);
        Assert.Equal("pcs", byName[0].UnitName);

        var byCode = await _db.Product.SearchAsync("8991234567890", 10);
        Assert.Single(byCode);

        var empty = await _db.Product.SearchAsync("tidak-ada", 10);
        Assert.Empty(empty);
    }

    [Fact]
    public async Task Negative_stock_allowed_setting_lets_sale_pass()
    {
        await using var connection = await _db.OpenAsync();
        await connection.ExecuteAsync(
            "DELETE FROM stock_movements WHERE product_id = @id;",
            new { id = _productId });

        await _db.Settings.SetAsync("pos.negative_stock", "allow");

        var result = await _db.Sale.CheckoutAsync(new CheckoutRequest(
            [new SaleLineInput(_productId, _unitId, 10_000, null,
                DiscountKind.None, 0)],
            "pmt-cash",
            200_000));

        Assert.Equal("closed", result.Status);
    }
}
