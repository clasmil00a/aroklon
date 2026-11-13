using Pos.Domain;
using Xunit;

namespace Pos.Data.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Fresh_database_requires_onboarding()
    {
        await using var db = await TestDatabase.CreateAsync();
        var bootstrap = await db.Auth.GetBootstrapAsync();

        Assert.True(bootstrap.NeedsOnboarding);
        Assert.Null(bootstrap.Session);
        Assert.Equal("My Shop", bootstrap.Company.Name);
        Assert.Equal(2, bootstrap.PriceLists.Count);
        Assert.Equal(4, bootstrap.PaymentTypes.Count);
    }

    [Fact]
    public async Task Onboarding_creates_admin_and_starts_session()
    {
        await using var db = await TestDatabase.CreateAsync();

        var session = await db.CreateAdminAsync(shopName: "Toko Makmur");

        Assert.Equal("admin", session.Username);
        Assert.True(session.IsAdmin);
        Assert.Same(session, db.Sessions.Current);

        var bootstrap = await db.Auth.GetBootstrapAsync();
        Assert.False(bootstrap.NeedsOnboarding);
        Assert.Equal("Toko Makmur", bootstrap.Company.Name);
        Assert.Same(session, bootstrap.Session);
    }

    [Fact]
    public async Task Second_onboarding_is_rejected()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => db.Auth.CreateFirstAdminAsync("Toko Lain", "Admin Lain", "admin2", "Admin1234"));

        Assert.Equal("auth.onboarding_complete", exception.Code);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password_and_resets_failures()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Auth.LogoutAsync();

        await Assert.ThrowsAsync<AuthException>(() => db.Auth.LoginAsync("admin", "salah-total"));

        var session = await db.Auth.LoginAsync("admin", "Admin1234");
        Assert.Equal("admin", session.Username);
        Assert.NotNull(db.Sessions.Current);

        await using var connection = await db.OpenAsync();
        var failedCount = await Dapper.SqlMapper.ExecuteScalarAsync<long>(
            connection,
            "SELECT failed_login_count FROM users WHERE username = 'admin';");
        Assert.Equal(0, failedCount);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_until_cooldown_passes()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Auth.LogoutAsync();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var invalid = await Assert.ThrowsAsync<AuthException>(
                () => db.Auth.LoginAsync("admin", "salah"));
            Assert.Equal(ErrorCodes.AuthInvalid, invalid.Code);
        }

        var locked = await Assert.ThrowsAsync<AuthException>(
            () => db.Auth.LoginAsync("admin", "salah"));
        Assert.Equal(ErrorCodes.AuthLocked, locked.Code);

        var stillLocked = await Assert.ThrowsAsync<AuthException>(
            () => db.Auth.LoginAsync("admin", "Admin1234"));
        Assert.Equal(ErrorCodes.AuthLocked, stillLocked.Code);

        db.Clock.Advance(TimeSpan.FromMinutes(16));

        var session = await db.Auth.LoginAsync("admin", "Admin1234");
        Assert.Equal("admin", session.Username);
    }

    [Fact]
    public async Task Unknown_and_inactive_users_are_rejected()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Auth.LogoutAsync();

        var unknown = await Assert.ThrowsAsync<AuthException>(
            () => db.Auth.LoginAsync("tidak-ada", "Admin1234"));
        Assert.Equal(ErrorCodes.AuthInvalid, unknown.Code);

        var cashier = await db.CreateUserAndLoginAsync("role-cashier", "kasir", "Kasir1234");
        await using (var connection = await db.OpenAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(
                connection,
                "UPDATE users SET active = 0 WHERE id = @id;",
                new { id = cashier.UserId });
        }

        await db.Auth.LogoutAsync();

        var inactive = await Assert.ThrowsAsync<AuthException>(
            () => db.Auth.LoginAsync("kasir", "Kasir1234"));
        Assert.Equal("auth.inactive", inactive.Code);
    }

    [Fact]
    public async Task Password_change_requires_current_password_and_invalidates_old_one()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        var wrong = await Assert.ThrowsAsync<AuthException>(
            () => db.Auth.ChangePasswordAsync("salah", "Baru12345"));
        Assert.Equal(ErrorCodes.AuthInvalid, wrong.Code);

        await db.Auth.ChangePasswordAsync("Admin1234", "Baru12345");
        await db.Auth.LogoutAsync();

        await Assert.ThrowsAsync<AuthException>(() => db.Auth.LoginAsync("admin", "Admin1234"));

        var session = await db.Auth.LoginAsync("admin", "Baru12345");
        Assert.Equal("admin", session.Username);
    }

    [Fact]
    public async Task Logout_clears_the_session()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        await db.Auth.LogoutAsync();

        Assert.Null(db.Sessions.Current);
    }

    [Fact]
    public async Task Onboarding_validates_shop_user_and_password()
    {
        await using var db = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => db.Auth.CreateFirstAdminAsync("X", "Admin Uji", "admin", "Admin1234"));
        await Assert.ThrowsAsync<ValidationException>(
            () => db.Auth.CreateFirstAdminAsync("Toko Uji", "A", "admin", "Admin1234"));
        await Assert.ThrowsAsync<ValidationException>(
            () => db.Auth.CreateFirstAdminAsync("Toko Uji", "Admin Uji", "ab", "Admin1234"));
        await Assert.ThrowsAsync<ValidationException>(
            () => db.Auth.CreateFirstAdminAsync("Toko Uji", "Admin Uji", "admin", "pendek"));
        await Assert.ThrowsAsync<ValidationException>(
            () => db.Auth.CreateFirstAdminAsync("Toko Uji", "Admin Uji", "admin", "admin"));

        Assert.True((await db.Auth.GetBootstrapAsync()).NeedsOnboarding);
    }
}
