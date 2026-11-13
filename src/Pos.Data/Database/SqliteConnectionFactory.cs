using Dapper;
using Microsoft.Data.Sqlite;

namespace Pos.Data.Database;

public interface IDbConnectionFactory
{
    string DatabasePath { get; }

    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Lokasi database wajib diisi.", nameof(databasePath));
        }

        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();

        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }
}
