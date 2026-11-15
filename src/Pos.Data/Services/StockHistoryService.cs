using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Models;
using Pos.Domain.Time;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for stock history per product.
/// When enabled, shows stock movement history per product.
/// </summary>
public sealed class StockHistoryService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public StockHistoryService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'stock.history_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Pos.Data.Services.SqlText.DisplaySetting(json) == "true";
    }

    public async Task<IReadOnlyList<StockHistoryEntry>> GetHistoryAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<StockHistoryRow>(new CommandDefinition(
            """
            SELECT sm.id, sm.type, sm.qty_delta_base_milli, sm.reason, sm.created_at,
                   u.full_name AS user_name
            FROM stock_movements sm
            LEFT JOIN users u ON u.id = sm.user_id
            WHERE sm.product_id = @productId
            ORDER BY sm.created_at DESC
            LIMIT 100;
            """,
            new { productId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new StockHistoryEntry(
            r.Id, r.Type, r.QtyDeltaBaseMilli, r.Reason ?? string.Empty,
            r.CreatedAt, r.UserName ?? string.Empty)).ToList();
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class StockHistoryRow
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long QtyDeltaBaseMilli { get; set; }
        public string? Reason { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string? UserName { get; set; }
    }
}

/// <summary>Stock history entry.</summary>
public sealed record StockHistoryEntry(
    string Id,
    string Type,
    long QtyDeltaBaseMilli,
    string Reason,
    string CreatedAt,
    string UserName);
