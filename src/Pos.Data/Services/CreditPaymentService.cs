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
/// Stub service for credit payments and outstanding balance.
/// When enabled, allows sales with unpaid balances (credit sales).
/// </summary>
public sealed class CreditPaymentService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CreditPaymentService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'pos.credit_payments_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Pos.Data.Services.SqlText.DisplaySetting(json) == "true";
    }

    public async Task<IReadOnlyList<CreditBalance>> GetOutstandingBalancesAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CreditBalanceRow>(new CommandDefinition(
            """
            SELECT c.id AS CustomerId, c.name AS CustomerName,
                   COALESCE(SUM(s.balance_minor), 0) AS OutstandingBalance
            FROM customers c
            JOIN sales s ON s.customer_id = c.id
            WHERE s.balance_minor > 0
            GROUP BY c.id, c.name
            ORDER BY OutstandingBalance DESC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new CreditBalance(r.CustomerId, r.CustomerName, r.OutstandingBalance)).ToList();
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class CreditBalanceRow
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public long OutstandingBalance { get; set; }
    }
}

/// <summary>Outstanding credit balance per customer.</summary>
public sealed record CreditBalance(string CustomerId, string CustomerName, long OutstandingBalance);
