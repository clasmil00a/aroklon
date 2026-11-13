using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;

namespace Pos.Data.Services;

public sealed class ProductService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly Domain.Time.IClock _clock;

    public ProductService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        Domain.Time.IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProductLookup>> SearchAsync(
        string? query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PosSell) && !session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mencari produk.");
        }

        var register = await GetRegisterAsync(cancellationToken).ConfigureAwait(false);
        var search = (query ?? string.Empty).Trim();
        var cappedLimit = Math.Clamp(limit, 1, 100);
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ProductRow>(new CommandDefinition(
            """
            SELECT
              p.id AS ProductId,
              p.name AS Name,
              p.code AS Code,
              pu.id AS ProductUnitId,
              bpu.id AS BaseUnitId,
              u.name AS UnitName,
              pu.factor_to_base_milli AS UnitFactorMilli,
              COALESCE(pl.price_minor, pu.price_minor,
                       (p.price_minor * pu.factor_to_base_milli + 500) / 1000) AS PriceMinor,
              (p.cost_minor * pu.factor_to_base_milli + 500) / 1000 AS CostMinor,
              COALESCE(st.qty_base_milli, 0) AS StockBaseMilli,
              p.is_service AS IsService,
              p.price_change_allowed AS PriceChangeAllowed,
              NULL AS Barcode,
              g.name AS GroupName
            FROM products p
            JOIN product_units pu ON pu.id = (
              SELECT pu2.id
              FROM product_units pu2
              WHERE pu2.product_id = p.id AND pu2.active = 1
              ORDER BY pu2.is_default_sale DESC, pu2.is_base DESC, pu2.sort_order
              LIMIT 1
            )
            JOIN product_units bpu ON bpu.product_id = p.id AND bpu.is_base = 1
            JOIN units u ON u.id = pu.unit_id
            JOIN units bu ON bu.id = bpu.unit_id
            LEFT JOIN product_groups g ON g.id = p.group_id
            LEFT JOIN price_list_items pl
              ON pl.product_unit_id = pu.id AND pl.price_list_id = @priceListId
            LEFT JOIN v_stock_on_hand st ON st.product_id = p.id
            WHERE p.active = 1
              AND (
                @search = ''
                OR p.name LIKE @like
                OR p.code LIKE @like
                OR EXISTS (
                  SELECT 1
                  FROM product_barcodes pb
                  JOIN product_units pbu ON pbu.id = pb.product_unit_id
                  WHERE pbu.product_id = p.id AND pb.barcode LIKE @like
                )
              )
            ORDER BY
              CASE WHEN p.code = @search COLLATE NOCASE THEN 0
                   WHEN p.name = @search COLLATE NOCASE THEN 1
                   ELSE 2 END,
              p.name
            LIMIT @limit;
            """,
            new
            {
                search,
                like = "%" + search + "%",
                priceListId = register.DefaultPriceListId,
                limit = cappedLimit
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(row => row.ToLookup()).ToList();
    }

    public async Task<ProductLookup?> FindByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var register = await GetRegisterAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(new CommandDefinition(
            """
            SELECT
              p.id AS ProductId,
              p.name AS Name,
              p.code AS Code,
              pu.id AS ProductUnitId,
              bpu.id AS BaseUnitId,
              u.name AS UnitName,
              pu.factor_to_base_milli AS UnitFactorMilli,
              COALESCE(pl.price_minor, pu.price_minor,
                       (p.price_minor * pu.factor_to_base_milli + 500) / 1000) AS PriceMinor,
              (p.cost_minor * pu.factor_to_base_milli + 500) / 1000 AS CostMinor,
              COALESCE(st.qty_base_milli, 0) AS StockBaseMilli,
              p.is_service AS IsService,
              p.price_change_allowed AS PriceChangeAllowed,
              pb.barcode AS Barcode,
              g.name AS GroupName
            FROM product_barcodes pb
            JOIN product_units pu ON pu.id = pb.product_unit_id AND pu.active = 1
            JOIN products p ON p.id = pu.product_id AND p.active = 1
            JOIN product_units bpu ON bpu.product_id = p.id AND bpu.is_base = 1
            JOIN units u ON u.id = pu.unit_id
            JOIN units bu ON bu.id = bpu.unit_id
            LEFT JOIN product_groups g ON g.id = p.group_id
            LEFT JOIN price_list_items pl
              ON pl.product_unit_id = pu.id AND pl.price_list_id = @priceListId
            LEFT JOIN v_stock_on_hand st ON st.product_id = p.id
            WHERE pb.barcode = @barcode COLLATE NOCASE
            LIMIT 1;
            """,
            new { barcode = barcode.Trim(), priceListId = register.DefaultPriceListId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToLookup();
    }

    public async Task<ProductLookup> GetAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var register = await GetRegisterAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(new CommandDefinition(
            """
            SELECT
              p.id AS ProductId,
              p.name AS Name,
              p.code AS Code,
              pu.id AS ProductUnitId,
              bpu.id AS BaseUnitId,
              u.name AS UnitName,
              pu.factor_to_base_milli AS UnitFactorMilli,
              COALESCE(pl.price_minor, pu.price_minor,
                       (p.price_minor * pu.factor_to_base_milli + 500) / 1000) AS PriceMinor,
              (p.cost_minor * pu.factor_to_base_milli + 500) / 1000 AS CostMinor,
              COALESCE(st.qty_base_milli, 0) AS StockBaseMilli,
              p.is_service AS IsService,
              p.price_change_allowed AS PriceChangeAllowed,
              NULL AS Barcode,
              g.name AS GroupName
            FROM products p
            JOIN product_units pu ON pu.id = (
              SELECT pu2.id
              FROM product_units pu2
              WHERE pu2.product_id = p.id AND pu2.active = 1
              ORDER BY pu2.is_default_sale DESC, pu2.is_base DESC, pu2.sort_order
              LIMIT 1
            )
            JOIN product_units bpu ON bpu.product_id = p.id AND bpu.is_base = 1
            JOIN units u ON u.id = pu.unit_id
            JOIN units bu ON bu.id = bpu.unit_id
            LEFT JOIN product_groups g ON g.id = p.group_id
            LEFT JOIN price_list_items pl
              ON pl.product_unit_id = pu.id AND pl.price_list_id = @priceListId
            LEFT JOIN v_stock_on_hand st ON st.product_id = p.id
            WHERE p.id = @productId AND p.active = 1;
            """,
            new { productId, priceListId = register.DefaultPriceListId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToLookup()
            ?? throw new NotFoundException("product.not_found", "Produk tidak ditemukan.");
    }

    public async Task<ProductLookup> CreateAsync(
        ProductCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk menambah produk.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        var productId = Ids.NewId();
        var unitId = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO products
                  (id, name, code, active, default_qty_milli, is_service, cost_minor,
                   price_minor, price_change_allowed, created_at, updated_at)
                VALUES
                  (@id, @name, @code, 1, 1000, @isService, @cost,
                   @price, @priceChangeAllowed, @now, @now);
                """,
                new
                {
                    id = productId,
                    name = request.Name.Trim(),
                    code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
                    isService = request.IsService ? 1 : 0,
                    cost = request.CostMinor,
                    price = request.PriceMinor,
                    priceChangeAllowed = request.PriceChangeAllowed ? 1 : 0,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_units
                  (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale,
                   price_minor, sort_order, active, created_at, updated_at)
                VALUES
                  (@id, @productId, @unitId, 1000, 1, 1, @price, 0, 1, @now, @now);
                """,
                new
                {
                    id = unitId,
                    productId,
                    unitId = request.BaseUnitId,
                    price = request.PriceMinor,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.Barcode))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_barcodes (id, product_unit_id, barcode, is_primary, created_at)
                    VALUES (@id, @unitId, @barcode, 1, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        unitId,
                        barcode = request.Barcode.Trim(),
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            if (!request.IsService && request.OpeningStockBaseMilli > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO stock_movements
                      (id, product_id, type, qty_delta_base_milli, unit_cost_minor,
                       reference_type, reference_id, note, user_id, created_at)
                    VALUES
                      (@id, @productId, 'opening', @qty, @cost,
                       'product', @productId, 'Stok awal', @userId, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        productId,
                        qty = request.OpeningStockBaseMilli,
                        cost = request.CostMinor,
                        userId = session.UserId,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "product.created",
                "product",
                productId,
                new { name = request.Name.Trim(), price = request.PriceMinor },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException(
                "product.duplicate",
                "Kode atau barcode sudah dipakai produk lain.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return await GetAsync(productId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(string Id, string Name, string Symbol)>> ListUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<(string Id, string Name, string Symbol)>(
            new CommandDefinition(
                "SELECT id AS Id, name AS Name, COALESCE(symbol, name) AS Symbol FROM units WHERE active = 1 ORDER BY name;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private async Task<RegisterRow> GetRegisterAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<RegisterRow>(new CommandDefinition(
                   """
                   SELECT id AS Id, name AS Name, default_price_list_id AS DefaultPriceListId
                   FROM registers WHERE active = 1 ORDER BY created_at LIMIT 1;
                   """,
                   cancellationToken: cancellationToken)).ConfigureAwait(false)
               ?? throw new PosException(ErrorCodes.Database, "Register aktif tidak ditemukan.");
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static void Validate(ProductCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama produk minimal 2 karakter.");
        }

        if (request.Name.Trim().Length > 120)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama produk maksimal 120 karakter.");
        }

        if (string.IsNullOrWhiteSpace(request.BaseUnitId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Satuan dasar wajib dipilih.");
        }

        if (request.PriceMinor < 0 || request.CostMinor < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Harga dan biaya tidak boleh negatif.");
        }

        if (request.OpeningStockBaseMilli < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Stok awal tidak boleh negatif.");
        }
    }

    private sealed class RegisterRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DefaultPriceListId { get; set; }
    }

    private sealed class ProductRow
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string ProductUnitId { get; set; } = string.Empty;
        public string BaseUnitId { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public long UnitFactorMilli { get; set; }
        public long PriceMinor { get; set; }
        public long CostMinor { get; set; }
        public long StockBaseMilli { get; set; }
        public bool IsService { get; set; }
        public bool PriceChangeAllowed { get; set; }
        public string? Barcode { get; set; }
        public string? GroupName { get; set; }

        public ProductLookup ToLookup() => new(
            ProductId,
            Name,
            Code,
            ProductUnitId,
            BaseUnitId,
            UnitName,
            UnitFactorMilli,
            PriceMinor,
            CostMinor,
            StockBaseMilli,
            IsService,
            PriceChangeAllowed,
            Barcode,
            GroupName);
    }
}
