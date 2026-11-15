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
/// Stub service for split/merge/transfer items between orders.
/// When enabled, allows moving cart lines between sales.
/// </summary>
public sealed class CartSplitMergeService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CartSplitMergeService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'pos.split_merge_items_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Pos.Data.Services.SqlText.DisplaySetting(json) == "true";
    }

    public async Task<IReadOnlyList<OpenOrderSummary>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<OpenOrderRow>(new CommandDefinition(
            """
            SELECT s.id AS Id, s.number AS Number, s.created_at AS CreatedAt,
                   s.customer_name_snapshot AS CustomerName,
                   s.total_minor AS TotalMinor
            FROM sales s
            WHERE s.status = 'open'
            ORDER BY s.created_at DESC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new OpenOrderSummary(
            r.Id, r.Number, r.CreatedAt, r.CustomerName ?? string.Empty, r.TotalMinor)).ToList();
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class OpenOrderRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public long TotalMinor { get; set; }
    }
}

/// <summary>Open order summary for split/merge.</summary>
public sealed record OpenOrderSummary(
    string Id,
    string Number,
    string CreatedAt,
    string CustomerName,
    long TotalMinor);
