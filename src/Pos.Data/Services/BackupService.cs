using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Time;

namespace Pos.Data.Services;

/// <summary>
/// System backup job. The UI does not call this directly; it runs at startup
/// and at day close, and records every attempt in backup_runs.
/// </summary>
public sealed class BackupService
{
    private readonly IDbConnectionFactory _factory;
    private readonly DatabaseOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IDbConnectionFactory factory,
        DatabaseOptions options,
        IClock clock,
        ILogger<BackupService> logger)
    {
        _factory = factory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task TryCreateDailyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateDailyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Daily backup failed");
        }
    }

    public async Task<BackupRun> CreateDailyAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.BackupDirectory);
        var now = _clock.UtcNow;
        var since = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await connection.QuerySingleOrDefaultAsync<BackupRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, path AS Path, size_bytes AS SizeBytes, status AS Status,
                           error AS Error, created_at AS CreatedAt
                    FROM backup_runs
                    WHERE status = 'success' AND created_at >= @since
                    ORDER BY created_at DESC LIMIT 1;
                    """,
                    new { since = SqlText.Now(since) },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Map(existing);
        }

        var stamp = now.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(_options.BackupDirectory, $"pos-{stamp}.db");
        var counter = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(_options.BackupDirectory, $"pos-{stamp}-{counter}.db");
            counter++;
        }

        var id = Ids.NewId();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "VACUUM INTO @path;",
                new { path },
                commandTimeout: 180,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var size = new FileInfo(path).Length;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO backup_runs (id, path, size_bytes, status, error, created_at)
                VALUES (@id, @path, @size, 'success', NULL, @now);
                """,
                new { id, path, size, now = SqlText.Now(now) },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            _logger.LogInformation("Backup created at {Path} ({Size} bytes)", path, size);
            await ApplyRetentionAsync(connection, now, cancellationToken).ConfigureAwait(false);

            return new BackupRun(id, path, size, "success", null, SqlText.Now(now));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Backup failed for {Path}", path);
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO backup_runs (id, path, size_bytes, status, error, created_at)
                    VALUES (@id, @path, 0, 'failed', @error, @now);
                    """,
                    new { id, path, error = exception.Message, now = SqlText.Now(now) },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception auditException)
            {
                _logger.LogWarning(auditException, "Could not record failed backup");
            }

            throw new PosException(ErrorCodes.Database, "Cadangan database gagal dibuat.", exception);
        }
    }

    private async Task ApplyRetentionAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var retentionDays = await GetRetentionDaysAsync(connection, cancellationToken).ConfigureAwait(false);
        var cutoff = now.AddDays(-retentionDays);
        var rows = (await connection.QueryAsync<BackupRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, path AS Path, size_bytes AS SizeBytes, status AS Status,
                           error AS Error, created_at AS CreatedAt
                    FROM backup_runs
                    WHERE status = 'success'
                    ORDER BY created_at DESC;
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        foreach (var row in rows.Skip(30))
        {
            var createdAt = SqlText.ParseTime(row.CreatedAt);
            if (createdAt is null || createdAt >= cutoff)
            {
                continue;
            }

            try
            {
                if (File.Exists(row.Path))
                {
                    File.Delete(row.Path);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM backup_runs WHERE id = @id;",
                    new { id = row.Id },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not remove old backup {Path}", row.Path);
            }
        }
    }

    private static async Task<int> GetRetentionDaysAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var json = await connection.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    "SELECT value_json FROM settings WHERE key = 'backup.retention_days' AND scope = 'global' AND scope_id = '';",
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
        {
            return 30;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetInt32(out var days) && days > 0 ? days : 30;
        }
        catch (JsonException)
        {
            return 30;
        }
    }

    private static BackupRun Map(BackupRow row) =>
        new(row.Id, row.Path, row.SizeBytes, row.Status, row.Error, row.CreatedAt);

    private sealed class BackupRow
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
