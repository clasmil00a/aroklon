using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Triggers the cash drawer kick (ESC/POS pulse to drawer trigger pin).
/// Respects the pos.cash_drawer_enabled setting: when off, the kick is a no-op.
/// </summary>
public sealed class CashDrawerService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CashDrawerService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    /// <summary>
    /// Kicks the cash drawer. Requires an active session and the
    /// pos.cash_drawer_enabled setting to be true.
    /// </summary>
    public async Task KickAsync(CancellationToken cancellationToken = default)
    {
        _ = _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

        var enabled = await IsEnabledAsync(cancellationToken).ConfigureAwait(false);
        if (!enabled)
        {
            // Drawer kick is turned off in settings. Silent no-op.
            return;
        }

        // TODO: Hook up ESC/POS printer command here when a thermal printer
        // is connected. Example: printer.Write(new byte[] { 0x1B, 0x70, 0x00, 0x20, 0x20 });
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true when the pos.cash_drawer_enabled setting is true.
    /// </summary>
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'pos.cash_drawer_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return string.Equals(SqlText.DisplaySetting(json), "true", StringComparison.OrdinalIgnoreCase);
    }
}
