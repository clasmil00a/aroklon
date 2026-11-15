using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Manages stock adjustments (quick inventory count and correction).
/// Each adjustment creates stock movements with type 'adjustment'.
/// </summary>
public sealed class StockAdjustmentService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public StockAdjustmentService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    /// <summary>
    /// Gets current on-hand quantity for a product in base milli units.
    /// </summary>
    public async Task<long> GetOnHandAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COALESCE(SUM(qty_delta_base_milli), 0) FROM stock_movements WHERE product_id = @productId;",
            new { productId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists recent stock movements for a product.
    /// </summary>
    public async Task<IReadOnlyList<StockMovementInfo>> GetMovementsAsync(
        string productId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<StockMovementRow>(new CommandDefinition(
            """
            SELECT sm.id, sm.type, sm.qty_delta_base_milli, sm.reason, sm.created_at,
                   u.full_name AS user_name
            FROM stock_movements sm
            LEFT JOIN users u ON u.id = sm.user_id
            WHERE sm.product_id = @productId
            ORDER BY sm.created_at DESC
            LIMIT @limit;
            """,
            new { productId, limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new StockMovementInfo(
            r.Id, r.Type, r.QtyDeltaBaseMilli, r.Reason ?? string.Empty,
            r.CreatedAt, r.UserName ?? string.Empty)).ToList();
    }

    /// <summary>
    /// Adjusts stock to a target quantity. Creates a stock movement with the delta.
    /// </summary>
    public async Task AdjustStockAsync(
        string productId,
        long targetQtyBaseMilli,
        string reason,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.StockManage);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException(ErrorCodes.Validation, "Alasan penyesuaian stok wajib diisi.");

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var current = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COALESCE(SUM(qty_delta_base_milli), 0) FROM stock_movements WHERE product_id = @productId;",
                new { productId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var delta = targetQtyBaseMilli - current;
            if (delta == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var movementId = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO stock_movements
                  (id, product_id, type, qty_delta_base_milli, reason, user_id, created_at, updated_at)
                VALUES
                  (@id, @productId, 'adjustment', @delta, @reason, @userId, @now, @now);
                """,
                new
                {
                    id = movementId,
                    productId,
                    delta,
                    reason,
                    userId = session.UserId,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "stock.adjusted",
                "stock_movements",
                movementId,
                new { productId, delta, reason },
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

    /// <summary>
    /// Adds stock (receiving). Creates a stock movement with type 'receiving'.
    /// </summary>
    public async Task ReceiveStockAsync(
        string productId,
        long qtyBaseMilli,
        string reason,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.StockManage);

        if (qtyBaseMilli <= 0)
            throw new ValidationException(ErrorCodes.Validation, "Jumlah stok masuk harus lebih dari nol.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException(ErrorCodes.Validation, "Alasan stok masuk wajib diisi.");

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var movementId = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO stock_movements
                  (id, product_id, type, qty_delta_base_milli, reason, user_id, created_at, updated_at)
                VALUES
                  (@id, @productId, 'receiving', @qty, @reason, @userId, @now, @now);
                """,
                new
                {
                    id = movementId,
                    productId,
                    qty = qtyBaseMilli,
                    reason,
                    userId = session.UserId,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "stock.received",
                "stock_movements",
                movementId,
                new { productId, qty = qtyBaseMilli, reason },
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

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static void RequirePermission(UserSession session, string permission)
    {
        if (!session.Can(permission))
            throw new PermissionException("Anda tidak memiliki izin untuk menyesuaikan stok.");
    }

    private sealed class StockMovementRow
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long QtyDeltaBaseMilli { get; set; }
        public string? Reason { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string? UserName { get; set; }
    }
}

/// <summary>
/// Stock movement information for UI.
/// </summary>
public sealed record StockMovementInfo(
    string Id,
    string Type,
    long QtyDeltaBaseMilli,
    string Reason,
    string CreatedAt,
    string UserName);
