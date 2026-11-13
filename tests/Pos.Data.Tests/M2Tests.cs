using Dapper;
using Pos.Data.Tests;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Xunit;

namespace Pos.Data.Tests;

public sealed class UnitTests : IAsyncLifetime
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
    public async Task List_returns_all_units()
    {
        var units = await _db.Unit.ListAsync();
        Assert.Equal(12, units.Count);
        Assert.Contains(units, u => u.Name == "renteng");
        Assert.Contains(units, u => u.Name == "dus");
    }

    [Fact]
    public async Task Create_and_update_unit()
    {
        var created = await _db.Unit.CreateAsync(new("kotak", "ktk"));
        Assert.Equal("kotak", created.Name);
        Assert.Equal("ktk", created.Symbol);

        var all = await _db.Unit.ListAsync();
        Assert.Equal(13, all.Count);

        await _db.Unit.UpdateAsync(created.Id, new("kotak-kemasan", "ktk", false));
        var updated = (await _db.Unit.ListAsync()).Single(u => u.Id == created.Id);
        Assert.Equal("kotak-kemasan", updated.Name);
        Assert.Equal("ktk", updated.Symbol);
    }

    [Fact]
    public async Task Duplicate_unit_name_is_rejected()
    {
        await _db.Unit.CreateAsync(new("duplicate", "dup"));
        await Assert.ThrowsAsync<Pos.Domain.ConflictException>(() =>
            _db.Unit.CreateAsync(new("duplicate", "dup2")));
    }

    [Fact]
    public async Task Empty_unit_name_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.Unit.CreateAsync(new("   ", null)));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_units()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.Unit.CreateAsync(new("sate", "st")));
    }
}

public sealed class PriceListTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private string _productUnitId = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();

        await using var connection = await _db.OpenAsync();
        var now = Pos.Data.Services.SqlText.Now(_db.Clock.UtcNow);
        var productId = Pos.Domain.Ids.NewId();
        _productUnitId = Pos.Domain.Ids.NewId();

        await connection.ExecuteAsync(
            """
            INSERT OR IGNORE INTO units (id, name, symbol, active) VALUES ('unit-pcs', 'pcs', 'pcs', 1);
            INSERT INTO products (id, name, code, active, default_qty_milli, cost_minor, price_minor, price_change_allowed, created_at, updated_at)
            VALUES (@id, 'Beras 5kg', 'BER-001', 1, 1000, 8000, 12000, 1, @now, @now);
            INSERT INTO product_units (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale, price_minor, sort_order, active, created_at, updated_at)
            VALUES (@unitId, @id, 'unit-pcs', 1000, 1, 1, 12000, 0, 1, @now, @now);
            """,
            new { id = productId, unitId = _productUnitId, now });
    }

    [Fact]
    public async Task Create_and_list_price_lists()
    {
        var created = await _db.PriceList.CreateAsync(new("Grosir Toko", false));
        Assert.Equal("Grosir Toko", created.Name);
        Assert.False(created.ApplyPromotions);

        var lists = await _db.PriceList.ListAsync();
        Assert.Equal(3, lists.Count);
        Assert.Contains(lists, l => l.Name == "Grosir Toko");
    }

    [Fact]
    public async Task Set_and_clear_item_price()
    {
        await _db.PriceList.CreateAsync(new("Grosir", false));
        var list = (await _db.PriceList.ListActiveAsync()).Single(l => l.Name == "Grosir");

        await _db.PriceList.SetItemPriceAsync(list.Id, _productUnitId,
            new Pos.Domain.Models.PriceListItemUpdateRequest(9_000, null));

        var items = await _db.PriceList.GetItemsAsync(list.Id);
        Assert.Single(items);
        Assert.Equal(9_000, items[0].PriceMinor);
        Assert.False(items[0].IsInherited);

        // Clear override -> inherited.
        await _db.PriceList.SetItemPriceAsync(list.Id, _productUnitId,
            new Pos.Domain.Models.PriceListItemUpdateRequest(null, null));

        items = await _db.PriceList.GetItemsAsync(list.Id);
        Assert.Single(items);
        Assert.True(items[0].IsInherited);
        Assert.Equal(12_000, items[0].PriceMinor);
    }

    [Fact]
    public async Task Copy_price_list_duplicates_items()
    {
        await _db.PriceList.CreateAsync(new("Sumber", false));
        var source = (await _db.PriceList.ListAsync()).Single(l => l.Name == "Sumber");
        await _db.PriceList.SetItemPriceAsync(source.Id, _productUnitId,
            new Pos.Domain.Models.PriceListItemUpdateRequest(9_000, null));

        var copy = await _db.PriceList.CopyAsync(source.Id, "Salinan");
        var items = await _db.PriceList.GetItemsAsync(copy.Id);
        Assert.Single(items);
        Assert.Equal(9_000, items[0].PriceMinor);
        Assert.Equal("Salinan", copy.Name);
    }

    [Fact]
    public async Task Duplicate_price_list_name_is_rejected()
    {
        await _db.PriceList.CreateAsync(new("Promo", true));
        await Assert.ThrowsAsync<Pos.Domain.ConflictException>(async () =>
        {
            await _db.PriceList.CreateAsync(new("Promo", false));
        });
    }

    [Fact]
    public async Task Non_admin_cannot_manage_price_lists()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.PriceList.CreateAsync(new("X", true)));
    }
}

public sealed class ProductUnitTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private string _productId = null!;
    private string _baseUnitId = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();

        await using var connection = await _db.OpenAsync();
        var now = Pos.Data.Services.SqlText.Now(_db.Clock.UtcNow);
        _productId = Pos.Domain.Ids.NewId();
        _baseUnitId = "unit-pcs";

        await connection.ExecuteAsync(
            """
            INSERT OR IGNORE INTO units (id, name, symbol, active) VALUES
              (@baseId, 'pcs', 'pcs', 1),
              ('unit-rtg', 'renteng', 'rtg', 1);
            INSERT INTO products (id, name, code, active, default_qty_milli, cost_minor, price_minor, price_change_allowed, created_at, updated_at)
            VALUES (@id, 'Beras 5kg', 'BER-001', 1, 1000, 8000, 12000, 1, @now, @now);
            INSERT INTO product_units (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale, price_minor, sort_order, active, created_at, updated_at)
            VALUES ('unit-base', @id, @baseId, 1000, 1, 1, 12000, 0, 1, @now, @now);
            INSERT INTO stock_movements (id, product_id, type, qty_delta_base_milli, unit_cost_minor, reference_type, reference_id, note, user_id, created_at)
            VALUES ('sm-1', @id, 'opening', 10_000, 8000, 'product', @id, 'Stok awal', (SELECT id FROM users WHERE username='admin'), @now);
            """,
            new { id = _productId, baseId = _baseUnitId, now });
    }

    [Fact]
    public async Task Get_product_detail_returns_variants_and_stock()
    {
        var detail = await _db.Product.GetProductDetailAsync(_productId);
        Assert.Equal("Beras 5kg", detail.Name);
        Assert.Single(detail.Variants);
        Assert.True(detail.Variants[0].IsBase);
        Assert.Equal(10_000, detail.Variants[0].StockBaseMilli);
        Assert.Equal(10_000, detail.Variants[0].StockInSellingUnitMilli);
    }

    [Fact]
    public async Task Create_selling_variant_with_factor()
    {
        var variant = await _db.Product.CreateProductUnitAsync(new(
            _productId, "unit-rtg", 6_000, false, 72_000, 1));

        Assert.Equal("unit-rtg", variant.UnitId);
        Assert.Equal(6_000, variant.FactorToBaseMilli);
        Assert.Equal(72_000, variant.PriceMinor);
        Assert.False(variant.IsBase);

        var detail = await _db.Product.GetProductDetailAsync(_productId);
        Assert.Equal(2, detail.Variants.Count);
    }

    [Fact]
    public async Task Update_variant_changes_default_sale()
    {
        var variant = await _db.Product.CreateProductUnitAsync(new(
            _productId, "unit-rtg", 6_000, false, 72_000, 1));

        await _db.Product.UpdateProductUnitAsync(variant.ProductUnitId, new(
            null, true, 80_000, 2, true));

        var updated = await _db.Product.GetProductDetailAsync(_productId);
        var rtg = updated.Variants.Single(v => v.UnitId == "unit-rtg");
        Assert.True(rtg.IsDefaultSale);
        Assert.Equal(80_000, rtg.PriceMinor);
        Assert.Equal(2, rtg.SortOrder);

        // Base is no longer default sale.
        var baseVariant = updated.Variants.Single(v => v.IsBase);
        Assert.False(baseVariant.IsDefaultSale);
    }

    [Fact]
    public async Task Invalid_factor_is_rejected()
    {
        await Assert.ThrowsAsync<Pos.Domain.ValidationException>(() =>
            _db.Product.CreateProductUnitAsync(new(_productId, "unit-rtg", 0, false, null, 0)));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_product_units()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.Product.CreateProductUnitAsync(new(_productId, "unit-rtg", 6_000, false, null, 0)));
    }
}

public sealed class CustomerRegisterTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private string _customerId = null!;
    private string _registerId = null!;
    private string _wholesaleListId = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();

        await using var connection = await _db.OpenAsync();
        var now = Pos.Data.Services.SqlText.Now(_db.Clock.UtcNow);
        _customerId = Pos.Domain.Ids.NewId();
        _registerId = "register-1";
        _wholesaleListId = "pl-wholesale";

        await connection.ExecuteAsync(
            """
            INSERT INTO customers (id, code, name, type, price_list_id, discount_kind, discount_value, active, created_at, updated_at)
            VALUES (@id, 'C-001', 'Toko A', 'customer', NULL, 'none', 0, 1, @now, @now);
            """,
            new { id = _customerId, now });
    }

    [Fact]
    public async Task Update_customer_price_list_and_discount()
    {
        await _db.Customer.UpdateAsync(_customerId, new(
            _wholesaleListId, "percent", 500, true));

        var customer = (await _db.Customer.ListAsync()).Single(c => c.Id == _customerId);
        Assert.Equal(_wholesaleListId, customer.PriceListId);
        Assert.Equal("percent", customer.DiscountKind);
        Assert.Equal(500, customer.DiscountValue);
        Assert.True(customer.Active);
    }

    [Fact]
    public async Task Update_register_default_price_list()
    {
        await _db.Register.UpdateDefaultPriceListAsync(_registerId, new(_wholesaleListId));

        var register = (await _db.Register.ListAsync()).Single(r => r.Id == _registerId);
        Assert.Equal(_wholesaleListId, register.DefaultPriceListId);
        Assert.Equal("Wholesale", register.DefaultPriceListName);
    }

    [Fact]
    public async Task Customer_list_includes_inactive()
    {
        await _db.Customer.UpdateAsync(_customerId, new(null, null, null, false));
        var all = await _db.Customer.ListAsync();
        var activeOnly = await _db.Customer.ListActiveAsync();
        Assert.Single(all);
        Assert.Empty(activeOnly);
    }

    [Fact]
    public async Task Cashier_can_update_customer_price_list()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await _db.Customer.UpdateAsync(_customerId, new(_wholesaleListId, null, null, true));

        var customer = (await _db.Customer.ListAsync()).Single(c => c.Id == _customerId);
        Assert.Equal(_wholesaleListId, customer.PriceListId);
        Assert.True(customer.Active);
    }
}
