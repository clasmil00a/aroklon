using Dapper;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Xunit;

namespace Pos.Data.Tests;

public sealed class SettingsTests
{
    [Fact]
    public async Task Reading_settings_requires_a_session()
    {
        await using var db = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<PermissionException>(
            () => db.Settings.GetAsync("receipt.show_units"));
        await Assert.ThrowsAsync<PermissionException>(
            () => db.Settings.UpdateManyAsync(new Dictionary<string, object?>
            {
                ["receipt.show_units"] = true
            }));
    }

    [Fact]
    public async Task Admin_can_update_and_read_settings_as_json()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        await db.Settings.SetAsync("receipt.show_units", false);
        await db.Settings.UpdateManyAsync(new Dictionary<string, object?>
        {
            ["pos.quick_pay"] = new[] { 10_000, 20_000, 50_000 },
            ["shop.open_message"] = "Selamat datang"
        });

        var values = await db.Settings.GetManyAsync(new[]
        {
            "receipt.show_units",
            "pos.quick_pay",
            "shop.open_message",
            "tidak.ada"
        });

        Assert.Equal("false", values["receipt.show_units"]);
        Assert.Equal("[10000,20000,50000]", values["pos.quick_pay"]);
        Assert.Equal("Selamat datang", values["shop.open_message"]);
        Assert.False(values.ContainsKey("tidak.ada"));
    }

    [Fact]
    public async Task Cashier_cannot_change_settings_but_can_read_them()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Settings.SetAsync("receipt.show_units", true);
        await db.Auth.LogoutAsync();

        await db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");

        Assert.Equal("true", await db.Settings.GetAsync("receipt.show_units"));
        await Assert.ThrowsAsync<PermissionException>(
            () => db.Settings.SetAsync("receipt.show_units", false));
    }

    [Fact]
    public async Task Company_update_requires_settings_permission_and_valid_data()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => db.Company.UpdateAsync(new CompanyPatch("X", null, null, null, null, null, null, null)));
        await Assert.ThrowsAsync<ValidationException>(
            () => db.Company.UpdateAsync(new CompanyPatch("Toko Uji", null, null, null, null, "bukan-email", null, null)));

        var updated = await db.Company.UpdateAsync(new CompanyPatch(
            "Toko Uji",
            "CV Toko Uji",
            "01.234.567.8-901.000",
            "Jl. Merdeka 1, Jakarta",
            "021-555-0100",
            "toko@example.id",
            "Toko Uji\nJl. Merdeka 1",
            "Terima kasih telah berbelanja"));

        Assert.Equal("Toko Uji", updated.Name);
        Assert.Equal("CV Toko Uji", updated.LegalName);
        Assert.Equal("toko@example.id", updated.Email);

        await db.Auth.LogoutAsync();
        await db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");

        await Assert.ThrowsAsync<PermissionException>(
            () => db.Company.UpdateAsync(new CompanyPatch("Toko Lain", null, null, null, null, null, null, null)));

        var read = await db.Company.GetAsync();
        Assert.Equal("Toko Uji", read.Name);
    }

    [Fact]
    public async Task Settings_update_is_audited()
    {
        await using var db = await TestDatabase.CreateAsync();
        var admin = await db.CreateAdminAsync();

        await db.Settings.SetAsync("receipt.show_units", false);

        await using var connection = await db.OpenAsync();
        var actor = await connection.ExecuteScalarAsync<string?>(
            "SELECT actor_user_id FROM audit_events WHERE action = 'settings.updated' ORDER BY created_at DESC LIMIT 1;");
        Assert.Equal(admin.UserId, actor);
    }
}
