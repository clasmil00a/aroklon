using Pos.Data.Tests;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using System.Linq;
using Xunit;

namespace Pos.Data.Tests;

public sealed class PaymentTypeTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();
    }

    [Fact]
    public async Task Create_and_list_payment_types()
    {
        var created = await _db.PaymentTypes.CreateAsync(new(
            "Tunai", "banknote", "F1", true, true, true, 10));

        Assert.Equal("Tunai", created.Name);
        Assert.True(created.MarkAsPaid);
        Assert.True(created.Active);

        var list = await _db.PaymentTypes.ListAsync();
        Assert.Equal(5, list.Count); // 4 seed + 1 new
        Assert.Contains(list, t => t.Name == "Tunai");
    }

    [Fact]
    public async Task Update_payment_type()
    {
        var created = await _db.PaymentTypes.CreateAsync(new(
            "Kartu", "credit-card", "F2", true, false, false, 20));

        await _db.PaymentTypes.UpdateAsync(created.Id, new(
            "Kartu Kredit", null, null, false, true, true, false, null));

        var updated = (await _db.PaymentTypes.ListAsync()).Single(t => t.Id == created.Id);
        Assert.Equal("Kartu Kredit", updated.Name);
        Assert.False(updated.MarkAsPaid);
        Assert.True(updated.IsDefault);
        Assert.False(updated.Active);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        await _db.PaymentTypes.CreateAsync(new("Duplikat", null, null, true, true, true, 1));
        await Assert.ThrowsAsync<Pos.Domain.ConflictException>(() =>
            _db.PaymentTypes.CreateAsync(new("Duplikat", null, null, true, true, true, 2)));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_payment_types()
    {
        await _db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await Assert.ThrowsAsync<Pos.Domain.PermissionException>(() =>
            _db.PaymentTypes.CreateAsync(new("X", null, null, true, true, true, 1)));
    }
}

public sealed class UserManagementTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();
    }

    [Fact]
    public async Task Create_and_list_users()
    {
        var roles = await _db.Users.ListRolesAsync();
        Assert.NotEmpty(roles);
        var roleId = roles.First().Id;

        var created = await _db.Users.CreateAsync(new(
            "kasir2", "Kasir Dua", "KasirDua1234", roleId, true));

        Assert.Equal("kasir2", created.Username);
        Assert.Equal("Kasir Dua", created.FullName);
        Assert.Equal(roleId, created.RoleId);
        Assert.True(created.Active);

        var list = await _db.Users.ListAsync();
        Assert.Equal(2, list.Count); // admin + kasir2
    }

    [Fact]
    public async Task Toggle_user_active()
    {
        var roles = await _db.Users.ListRolesAsync();
        var roleId = roles.First().Id;

        var created = await _db.Users.CreateAsync(new(
            "kasir2", "Kasir Dua", "KasirDua1234", roleId, true));

        await _db.Users.ToggleActiveAsync(created.Id);

        var updated = (await _db.Users.ListAsync()).Single(u => u.Id == created.Id);
        Assert.False(updated.Active);

        await _db.Users.ToggleActiveAsync(created.Id);
        updated = (await _db.Users.ListAsync()).Single(u => u.Id == created.Id);
        Assert.True(updated.Active);
    }

    [Fact]
    public async Task Duplicate_username_is_rejected()
    {
        await _db.Users.CreateAsync(new("kasir2", "Kasir Dua", "KasirDua1234", "role-cashier", true));
        await Assert.ThrowsAsync<Pos.Domain.ConflictException>(() =>
            _db.Users.CreateAsync(new("kasir2", "Kasir Dua 2", "KasirDua1234", "role-cashier", true)));
    }
}

public sealed class DatabaseHealthTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public Task InitializeAsync() => SetUpAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SetUpAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await _db.CreateAdminAsync();
    }

    [Fact]
    public async Task Health_check_passes()
    {
        var health = await _db.Database.CheckHealthAsync();
        Assert.True(health.Connected);
        Assert.True(health.IntegrityOk);
        Assert.True(health.ForeignKeysOk);
        Assert.Null(health.Error);
    }

    [Fact]
    public async Task List_backups()
    {
        var backups = await _db.Database.ListBackupsAsync();
        // Fresh database may have no backups yet; just verify the query works
        Assert.True(backups != null);
    }

    [Fact]
    public async Task Get_and_set_backup_schedule()
    {
        var schedule = await _db.Database.GetBackupScheduleAsync();
        Assert.False(schedule.Enabled);
        Assert.Equal(2, schedule.Hour);
        Assert.Equal(0, schedule.Minute);

        await _db.Database.SetBackupScheduleAsync(new(true, 3, 30));

        var updated = await _db.Database.GetBackupScheduleAsync();
        Assert.True(updated.Enabled);
        Assert.Equal(3, updated.Hour);
        Assert.Equal(30, updated.Minute);
    }
}
