using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Models;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for lock order and proforma.
/// When enabled, orders can be locked to prevent modification.
/// </summary>
public sealed class OrderLockService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public OrderLockService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'pos.lock_order_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Pos.Data.Services.SqlText.DisplaySetting(json) == "true";
    }

    public async Task LockOrderAsync(
        string saleId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.PosVoid);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sales
                SET status = 'locked', void_reason = @reason, updated_at = @now
                WHERE id = @id AND status = 'open';
                """,
                new { id = saleId, reason, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

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
            throw new PermissionException("Anda tidak memiliki izin untuk mengunci pesanan.");
    }
}
