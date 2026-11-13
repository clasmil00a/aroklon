using Dapper;
using Microsoft.Extensions.Logging;
using Pos.Domain;

namespace Pos.Data.Database;

public sealed class DatabaseBootstrapper
{
    private readonly IDbConnectionFactory _factory;
    private readonly MigrationRunner _migrations;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseBootstrapper> _logger;

    public DatabaseBootstrapper(
        IDbConnectionFactory factory,
        MigrationRunner migrations,
        DatabaseOptions options,
        ILogger<DatabaseBootstrapper> logger)
    {
        _factory = factory;
        _migrations = migrations;
        _options = options;
        _logger = logger;
    }

    public async Task<DatabaseInfo> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_options.DatabasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var migration = await _migrations.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Database ready at {Path}, schema {Version}, migrations applied {Count}",
            _options.DatabasePath,
            migration.ToVersion,
            migration.Applied.Count);

        return new DatabaseInfo(_options.DatabasePath, migration.ToVersion);
    }

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var quickCheck = (await connection.QueryAsync<string>(
                new CommandDefinition("PRAGMA quick_check;", cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        if (quickCheck.Count != 1 || !string.Equals(quickCheck[0], "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new PosException(
                ErrorCodes.Database,
                "Pemeriksaan integritas database gagal. Pulihkan dari cadangan terakhir.");
        }

        var foreignKeyRows = (await connection.QueryAsync(
                new CommandDefinition("PRAGMA foreign_key_check;", cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        if (foreignKeyRows.Count > 0)
        {
            throw new PosException(
                ErrorCodes.Database,
                $"Database memiliki {foreignKeyRows.Count} pelanggaran kunci asing.");
        }
    }
}
