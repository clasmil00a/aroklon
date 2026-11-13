using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Pos.Data.Tests;

public sealed class AuditTests
{
    [Fact]
    public async Task Audit_events_are_append_only()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        await using var connection = await db.OpenAsync();

        var update = await Assert.ThrowsAsync<SqliteException>(() =>
            connection.ExecuteAsync("UPDATE audit_events SET action = 'diubah';"));
        Assert.Contains("append-only", update.Message, StringComparison.OrdinalIgnoreCase);

        var delete = await Assert.ThrowsAsync<SqliteException>(() =>
            connection.ExecuteAsync("DELETE FROM audit_events;"));
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);

        var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM audit_events;");
        Assert.True(count > 0);
    }

    [Fact]
    public async Task Onboarding_and_login_are_audited()
    {
        await using var db = await TestDatabase.CreateAsync();
        var admin = await db.CreateAdminAsync();
        await db.Auth.LogoutAsync();
        await db.Auth.LoginAsync("admin", "Admin1234");

        await using var connection = await db.OpenAsync();
        var actions = (await connection.QueryAsync<string>(
                "SELECT action FROM audit_events WHERE actor_user_id = @id ORDER BY created_at;",
                new { id = admin.UserId }))
            .ToList();

        Assert.Contains("auth.first_admin_created", actions);
        Assert.Contains("auth.logout", actions);
        Assert.Contains("auth.login_succeeded", actions);
    }

    [Fact]
    public async Task Failed_login_is_audited_without_leaking_the_password()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Auth.LogoutAsync();

        await Assert.ThrowsAsync<Pos.Domain.AuthException>(
            () => db.Auth.LoginAsync("admin", "sangat-rahasia"));

        await using var connection = await db.OpenAsync();
        var details = await connection.ExecuteScalarAsync<string>(
            "SELECT details_json FROM audit_events WHERE action = 'auth.login_failed' ORDER BY created_at DESC LIMIT 1;");

        Assert.NotNull(details);
        Assert.Contains("admin", details);
        Assert.DoesNotContain("sangat-rahasia", details);
    }
}
