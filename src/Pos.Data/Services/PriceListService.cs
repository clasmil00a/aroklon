using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class PriceListService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public PriceListService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PriceListSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<PriceListRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, apply_promotions AS ApplyPromotions, active AS Active
            FROM price_lists
            ORDER BY name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new PriceListSummary(r.Id, r.Name, r.ApplyPromotions, r.Active)).ToList();
    }

    public async Task<IReadOnlyList<PriceListSummary>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<PriceListRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, apply_promotions AS ApplyPromotions, active AS Active
            FROM price_lists
            WHERE active = 1
            ORDER BY name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new PriceListSummary(r.Id, r.Name, r.ApplyPromotions, r.Active)).ToList();
    }

    public async Task<PriceListSummary> CreateAsync(
        PriceListCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PriceListsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola daftar harga.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        var id = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO price_lists (id, name, apply_promotions, active, created_at, updated_at)
                VALUES (@id, @name, @applyPromotions, 1, @now, @now);
                """,
                new
                {
                    id,
                    name = request.Name.Trim(),
                    applyPromotions = request.ApplyPromotions ? 1 : 0,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "price_list.created",
                "price_list",
                id,
                new { name = request.Name.Trim(), applyPromotions = request.ApplyPromotions },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("price_list.duplicate", "Nama daftar harga sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new PriceListSummary(id, request.Name.Trim(), request.ApplyPromotions, true);
    }

    public async Task<IReadOnlyList<PriceListItemView>> GetItemsAsync(
        string priceListId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<PriceListItemRow>(new CommandDefinition(
            """
            SELECT
              product_unit_id AS ProductUnitId,
              product_id AS ProductId,
              product_name AS ProductName,
              unit_name AS UnitName,
              factor_to_base_milli AS FactorToBaseMilli,
              price_minor AS PriceMinor,
              is_inherited AS IsInherited,
              markup_bp AS MarkupBp
            FROM v_price_list_prices
            WHERE price_list_id = @priceListId
            ORDER BY product_name, unit_name;
            """,
            new { priceListId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new PriceListItemView(
            r.ProductUnitId, r.ProductId, r.ProductName, r.UnitName, null, r.FactorToBaseMilli,
            r.PriceMinor, r.IsInherited, r.MarkupBp)).ToList();
    }

    public async Task SetItemPriceAsync(
        string priceListId,
        string productUnitId,
        PriceListItemUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PriceListsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola daftar harga.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Verify the product unit exists and belongs to an active product.
            var unitExists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM product_units pu
                JOIN products p ON p.id = pu.product_id
                WHERE pu.id = @productUnitId AND pu.active = 1 AND p.active = 1;
                """,
                new { productUnitId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (unitExists == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("product_unit.not_found", "Satuan produk tidak ditemukan.");
            }

            if (request.PriceMinor is null)
            {
                // Remove override -> inherit.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    DELETE FROM price_list_items
                    WHERE price_list_id = @priceListId AND product_unit_id = @productUnitId;
                    """,
                    new { priceListId, productUnitId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                if (request.PriceMinor.Value < 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw new ValidationException(ErrorCodes.Validation, "Harga tidak boleh negatif.");
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO price_list_items
                      (price_list_id, product_unit_id, price_minor, markup_bp, updated_at)
                    VALUES
                      (@priceListId, @productUnitId, @priceMinor, @markupBp, @now)
                    ON CONFLICT(price_list_id, product_unit_id) DO UPDATE SET
                      price_minor = excluded.price_minor,
                      markup_bp = excluded.markup_bp,
                      updated_at = excluded.updated_at;
                    """,
                    new
                    {
                        priceListId,
                        productUnitId,
                        priceMinor = request.PriceMinor.Value,
                        markupBp = request.MarkupBp,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "price_list.item_updated",
                "price_list_item",
                productUnitId,
                new { priceListId, priceMinor = request.PriceMinor, markupBp = request.MarkupBp },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<PriceListSummary> CopyAsync(
        string sourcePriceListId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PriceListsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola daftar harga.");
        }

        if (string.IsNullOrWhiteSpace(newName) || newName.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama daftar harga wajib diisi.");
        }

        var now = _clock.UtcNow;
        var newId = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var source = await connection.QuerySingleOrDefaultAsync<PriceListRow>(new CommandDefinition(
                """
                SELECT id AS Id, name AS Name, apply_promotions AS ApplyPromotions, active AS Active
                FROM price_lists WHERE id = @id;
                """,
                new { id = sourcePriceListId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (source is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("price_list.not_found", "Daftar harga sumber tidak ditemukan.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO price_lists (id, name, apply_promotions, active, created_at, updated_at)
                VALUES (@id, @name, @applyPromotions, 1, @now, @now);
                """,
                new
                {
                    id = newId,
                    name = newName.Trim(),
                    applyPromotions = source.ApplyPromotions,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO price_list_items
                  (price_list_id, product_unit_id, price_minor, markup_bp, updated_at)
                SELECT @newId, product_unit_id, price_minor, markup_bp, @now
                FROM price_list_items
                WHERE price_list_id = @sourceId;
                """,
                new { newId, sourceId = sourcePriceListId, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "price_list.copied",
                "price_list",
                newId,
                new { sourcePriceListId, newName = newName.Trim() },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new PriceListSummary(newId, newName.Trim(), source.ApplyPromotions, true);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("price_list.duplicate", "Nama daftar harga sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void Validate(PriceListCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama daftar harga wajib diisi.");
        }

        if (request.Name.Trim().Length > 80)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama daftar harga maksimal 80 karakter.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class PriceListRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool ApplyPromotions { get; set; }
        public bool Active { get; set; }
    }

    private sealed class PriceListItemRow
    {
        public string ProductUnitId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public long FactorToBaseMilli { get; set; }
        public long PriceMinor { get; set; }
        public bool IsInherited { get; set; }
        public int? MarkupBp { get; set; }
    }
}
