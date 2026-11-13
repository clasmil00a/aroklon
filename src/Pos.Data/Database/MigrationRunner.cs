using System.Globalization;
using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pos.Domain;

namespace Pos.Data.Database;

public sealed record MigrationResult(int FromVersion, int ToVersion, IReadOnlyList<string> Applied);

/// <summary>
/// Executes embedded SQL migrations. Migration 0001 is the full initial schema
/// and manages its own transaction; later migrations run inside a transaction
/// and must not contain BEGIN/COMMIT.
/// </summary>
public sealed class MigrationRunner
{
    private const string ResourcePrefix = "Pos.Data.Migrations.";
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IDbConnectionFactory factory, ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteScalarAsync<string>(
            new CommandDefinition("PRAGMA journal_mode = WAL;", cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var originalVersion = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        var fromVersion = originalVersion;
        var migrations = Discover();
        var applied = new List<string>();

        if (fromVersion == 0)
        {
            var initial = migrations.SingleOrDefault(m => m.Version == 1)
                ?? throw new PosException(ErrorCodes.Database, "Migrasi awal tidak ditemukan.");
            var sql = await ReadAsync(initial.ResourceName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Applying initial database migration {Name}", initial.Name);
            await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    commandTimeout: 180,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var after = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
            if (after < 1)
            {
                throw new PosException(ErrorCodes.Database, "Migrasi awal tidak menaikkan versi skema.");
            }

            applied.Add(initial.Name);
            fromVersion = after;
        }

        foreach (var migration in migrations
                     .Where(m => m.Version > fromVersion)
                     .OrderBy(m => m.Version))
        {
            var sql = await ReadAsync(migration.ResourceName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Applying database migration {Version} {Name}", migration.Version, migration.Name);

            await using var transaction = connection.BeginTransaction(deferred: false);
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                        sql,
                        transaction: transaction,
                        commandTimeout: 180,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(
                        $"PRAGMA user_version = {migration.Version};",
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }

            applied.Add(migration.Name);
            fromVersion = migration.Version;
        }

        return new MigrationResult(originalVersion, fromVersion, applied);
    }

    private static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var version = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("PRAGMA user_version;", cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return checked((int)version);
    }

    private static async Task<string> ReadAsync(string resourceName, CancellationToken cancellationToken)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new PosException(ErrorCodes.Database, $"Migrasi {resourceName} tidak ditemukan.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<Migration> Discover()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var migrations = new List<Migration>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = resource[ResourcePrefix.Length..^4];
            var separator = fileName.IndexOf('_', StringComparison.Ordinal);
            if (separator <= 0 ||
                !int.TryParse(fileName[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                continue;
            }

            var name = fileName[(separator + 1)..].Replace('_', ' ');
            migrations.Add(new Migration(version, name, resource));
        }

        return migrations.OrderBy(m => m.Version).ToList();
    }

    private sealed record Migration(int Version, string Name, string ResourceName);
}
