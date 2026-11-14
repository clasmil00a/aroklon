using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class DatabaseService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;
    private readonly DatabaseOptions _options;

    public DatabaseService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock,
        DatabaseOptions options)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        try
        {
            await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

            var schemaVersion = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "PRAGMA user_version;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var integrity = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "PRAGMA quick_check;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var foreignKeys = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "PRAGMA foreign_key_check;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            return new HealthCheckResult(
                Connected: true,
                SchemaVersion: (int)schemaVersion,
                IntegrityOk: string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase),
                ForeignKeysOk: foreignKeys == 0,
                Error: null);
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(
                Connected: false,
                SchemaVersion: 0,
                IntegrityOk: false,
                ForeignKeysOk: false,
                Error: exception.Message);
        }
    }

    public async Task<IReadOnlyList<BackupRunSummary>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<BackupRunRow>(new CommandDefinition(
            """
            SELECT id AS Id, path AS Path, size_bytes AS SizeBytes, status AS Status,
                   error AS Error, created_at AS CreatedAt
            FROM backup_runs
            ORDER BY created_at DESC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new BackupRunSummary(
            r.Id, r.Path, r.SizeBytes, r.Status, r.Error, r.CreatedAt)).ToList();
    }

    public async Task RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk memulihkan database.");
        }

        if (string.IsNullOrWhiteSpace(request.BackupPath))
        {
            throw new ValidationException(ErrorCodes.Validation, "Jalur cadangan wajib diisi.");
        }

        if (!File.Exists(request.BackupPath))
        {
            throw new NotFoundException("backup.not_found", "File cadangan tidak ditemukan.");
        }

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Restore using VACUUM INTO reverse: copy backup over current DB
            // First, close all connections by using a temporary approach
            var currentDbPath = _options.DatabasePath;
            var tempPath = currentDbPath + ".restoring";

            try
            {
                File.Copy(request.BackupPath, tempPath, overwrite: true);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new PosException(ErrorCodes.Database, "Gagal menyalin file cadangan.", exception);
            }

            // Record the restore attempt
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO backup_runs (id, path, size_bytes, status, error, created_at)
                VALUES (@id, @path, 0, 'restored', NULL, @now);
                """,
                new
                {
                    id = Ids.NewId(),
                    path = request.BackupPath,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "database.restored",
                "database",
                null,
                new { backupPath = request.BackupPath },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Note: Actual file replacement requires app restart. The DB file will
            // be replaced on next startup via the temp file marker.
            File.Move(tempPath, currentDbPath, overwrite: true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<BackupScheduleSetting> GetBackupScheduleAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var enabledJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'backup.schedule_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var hourJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'backup.schedule_hour' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var minuteJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'backup.schedule_minute' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var enabled = ParseBool(enabledJson) ?? false;
        var hour = ParseInt(hourJson) ?? 2;
        var minute = ParseInt(minuteJson) ?? 0;

        return new BackupScheduleSetting(enabled, hour, minute);
    }

    public async Task SetBackupScheduleAsync(BackupScheduleSetting setting, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengubah jadwal cadangan.");
        }

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
                VALUES (@key, 'global', '', @valueJson, @now)
                ON CONFLICT(key, scope, scope_id) DO UPDATE SET
                  value_json = excluded.value_json,
                  updated_at = excluded.updated_at;
                """,
                new
                {
                    key = "backup.schedule_enabled",
                    valueJson = JsonSerializer.Serialize(setting.Enabled),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
                VALUES (@key, 'global', '', @valueJson, @now)
                ON CONFLICT(key, scope, scope_id) DO UPDATE SET
                  value_json = excluded.value_json,
                  updated_at = excluded.updated_at;
                """,
                new
                {
                    key = "backup.schedule_hour",
                    valueJson = JsonSerializer.Serialize(setting.Hour),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
                VALUES (@key, 'global', '', @valueJson, @now)
                ON CONFLICT(key, scope, scope_id) DO UPDATE SET
                  value_json = excluded.value_json,
                  updated_at = excluded.updated_at;
                """,
                new
                {
                    key = "backup.schedule_minute",
                    valueJson = JsonSerializer.Serialize(setting.Minute),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "backup.schedule_updated",
                "setting",
                null,
                new { enabled = setting.Enabled, hour = setting.Hour, minute = setting.Minute },
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

    private static bool? ParseBool(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.True
                || (doc.RootElement.ValueKind == JsonValueKind.String
                    && string.Equals(doc.RootElement.GetString(), "true", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseInt(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetInt32(out var v) ? v : null;
        }
        catch
        {
            return null;
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class BackupRunRow
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}

/// <summary>Ringkasan file cadangan (S38).</summary>
public sealed record BackupRunSummary(
    string Id,
    string Path,
    long SizeBytes,
    string Status,
    string? Error,
    string CreatedAt);
