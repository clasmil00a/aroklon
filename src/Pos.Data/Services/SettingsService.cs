using Dapper;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;

namespace Pos.Data.Services;

public sealed class SettingsService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly Pos.Domain.Time.IClock _clock;

    public SettingsService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        Pos.Domain.Time.IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var requested = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requested.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<SettingRow>(
                new CommandDefinition(
                    """
                    SELECT key AS Key, value_json AS ValueJson
                    FROM settings
                    WHERE scope = 'global' AND scope_id = '' AND key IN @keys;
                    """,
                    new { keys = requested },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var value = SqlText.DisplaySetting(row.ValueJson);
            if (value is not null)
            {
                result[row.Key] = value;
            }
        }

        return result;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var values = await GetManyAsync(new[] { key }, cancellationToken).ConfigureAwait(false);
        return values.TryGetValue(key, out var value) ? value : null;
    }

    public Task SetAsync(string key, object? value, CancellationToken cancellationToken = default) =>
        UpdateManyAsync(new Dictionary<string, object?> { [key] = value }, cancellationToken);

    public async Task UpdateManyAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.SettingsManage);

        if (values.Count == 0)
        {
            return;
        }

        foreach (var key in values.Keys)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            {
                throw new ValidationException(ErrorCodes.Validation, "Kunci pengaturan tidak valid.");
            }
        }

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            foreach (var (key, value) in values)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
                    VALUES (@key, 'global', '', @valueJson, @now)
                    ON CONFLICT(key, scope, scope_id) DO UPDATE SET
                      value_json = excluded.value_json,
                      updated_at = excluded.updated_at;
                    """,
                    new { key, valueJson = SqlText.Serialize(value), now = SqlText.Now(now) },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "settings.updated",
                "settings",
                null,
                new { keys = values.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray() },
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
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengubah pengaturan.");
        }
    }

    private sealed class SettingRow
    {
        public string Key { get; set; } = string.Empty;
        public string ValueJson { get; set; } = "null";
    }
}
