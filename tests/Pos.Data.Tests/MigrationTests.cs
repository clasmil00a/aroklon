using Dapper;
using Xunit;

namespace Pos.Data.Tests;

public sealed class MigrationTests
{
    [Fact]
    public async Task Fresh_database_has_all_tables_views_and_triggers()
    {
        await using var db = await TestDatabase.CreateAsync();
        await using var connection = await db.OpenAsync();

        var tables = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';");
        var views = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'view';");
        var triggers = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger';");
        var version = await connection.ExecuteScalarAsync<long>("PRAGMA user_version;");

        Assert.Equal(35, tables);
        Assert.Equal(3, views);
        Assert.Equal(2, triggers);
        Assert.Equal(2, version);
    }

    [Fact]
    public async Task Seed_data_is_ready_for_first_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        await using var connection = await db.OpenAsync();

        Assert.Equal(12, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM units;"));
        Assert.Equal(3, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM roles;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM price_lists;"));
        Assert.Equal(4, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM payment_types;"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM registers;"));
        Assert.Equal(19, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM settings;"));
        Assert.Equal("My Shop", await connection.ExecuteScalarAsync<string>("SELECT name FROM company WHERE id = 'company';"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users;"));
    }

    [Fact]
    public async Task Migration_is_idempotent_and_integrity_checks_pass()
    {
        await using var db = await TestDatabase.CreateAsync();

        var second = await db.Bootstrapper.InitializeAsync();
        await db.Bootstrapper.VerifyAsync();

        Assert.Equal(2, second.SchemaVersion);
        await using var connection = await db.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("PRAGMA foreign_keys;"));
        Assert.Equal("wal", await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode;"));
        Assert.Empty(await connection.QueryAsync("PRAGMA foreign_key_check;"));
        Assert.Equal("ok", await connection.ExecuteScalarAsync<string>("PRAGMA quick_check;"));
    }
}
