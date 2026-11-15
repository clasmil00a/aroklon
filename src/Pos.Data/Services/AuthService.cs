using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;
using Pos.Data.Database;

namespace Pos.Data.Services;

public sealed class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IDbConnectionFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly ISessionStore _sessions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDbConnectionFactory factory,
        IPasswordHasher hasher,
        IClock clock,
        ISessionStore sessions,
        ILogger<AuthService> logger)
    {
        _factory = factory;
        _hasher = hasher;
        _clock = clock;
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<AppBootstrap> GetBootstrapAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var userCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("SELECT COUNT(*) FROM users;", cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var company = await connection.QuerySingleOrDefaultAsync<CompanyRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, name AS Name, legal_name AS LegalName, tax_number AS TaxNumber,
                           address AS Address, phone AS Phone, email AS Email, currency AS Currency,
                           currency_minor_unit AS CurrencyMinorUnit, locale AS Locale, timezone AS Timezone,
                           receipt_header AS ReceiptHeader, receipt_footer AS ReceiptFooter
                    FROM company WHERE id = 'company';
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)
            ?? throw new PosException(ErrorCodes.Database, "Identitas toko tidak ditemukan.");

        var register = await connection.QuerySingleOrDefaultAsync<RegisterRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, name AS Name, default_price_list_id AS DefaultPriceListId
                    FROM registers WHERE active = 1 ORDER BY created_at LIMIT 1;
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)
            ?? throw new PosException(ErrorCodes.Database, "Register aktif tidak ditemukan.");

        var priceLists = (await connection.QueryAsync<PriceListRow>(
                new CommandDefinition(
                    "SELECT id AS Id, name AS Name, apply_promotions AS ApplyPromotions FROM price_lists WHERE active = 1 ORDER BY name;",
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        var paymentTypes = (await connection.QueryAsync<PaymentTypeRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, name AS Name, icon AS Icon, shortcut AS Shortcut,
                           mark_as_paid AS MarkAsPaid, is_default AS IsDefault,
                           opens_cash_drawer AS OpensCashDrawer
                    FROM payment_types WHERE active = 1 ORDER BY sort_order;
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        var settingRows = (await connection.QueryAsync<SettingRow>(
                new CommandDefinition(
                    "SELECT key AS Key, value_json AS ValueJson FROM settings WHERE scope = 'global' AND scope_id = '';",
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in settingRows)
        {
            var value = SqlText.DisplaySetting(row.ValueJson);
            if (value is not null)
            {
                settings[row.Key] = value;
            }
        }

        return new AppBootstrap(
            userCount == 0,
            _sessions.Current,
            new CompanyInfo(
                company.Id,
                company.Name,
                company.LegalName,
                company.TaxNumber,
                company.Address,
                company.Phone,
                company.Email,
                company.LogoPath,
                company.Currency,
                company.CurrencyMinorUnit,
                company.Locale,
                company.Timezone,
                company.ReceiptHeader,
                company.ReceiptFooter),
            new RegisterInfo(register.Id, register.Name, register.DefaultPriceListId),
            priceLists.Select(x => new PriceListInfo(x.Id, x.Name, x.ApplyPromotions != 0)).ToList(),
            paymentTypes.Select(x => new PaymentTypeInfo(
                x.Id,
                x.Name,
                x.Icon,
                x.Shortcut,
                x.MarkAsPaid != 0,
                x.IsDefault != 0,
                x.OpensCashDrawer != 0)).ToList(),
            settings);
    }

    public async Task<UserSession> CreateFirstAdminAsync(
        string shopName,
        string fullName,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        shopName = ValidateShopName(shopName);
        fullName = ValidateFullName(fullName);
        username = ValidateUsername(username);
        PasswordPolicy.Validate(password);
        if (string.Equals(username, password, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("auth.password_weak", "Kata sandi tidak boleh sama dengan nama pengguna.");
        }

        var passwordHash = await _hasher.HashAsync(password, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        var userId = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var count = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT COUNT(*) FROM users;", transaction: transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            if (count > 0)
            {
                throw new ConflictException("auth.onboarding_complete", "Akun pertama sudah dibuat. Silakan masuk.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO users
                  (id, username, password_hash, full_name, role_id, active, failed_login_count, created_at, updated_at)
                VALUES
                  (@id, @username, @passwordHash, @fullName, 'role-admin', 1, 0, @now, @now);
                """,
                new { id = userId, username, passwordHash, fullName, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO company
                  (id, name, currency, currency_minor_unit, locale, timezone, receipt_footer, created_at, updated_at)
                VALUES
                  ('company', @shopName, 'IDR', 0, 'id-ID', 'Asia/Jakarta', 'Terima kasih', @now, @now)
                ON CONFLICT(id) DO UPDATE SET
                  name = excluded.name,
                  updated_at = excluded.updated_at;
                """,
                new { shopName, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                userId,
                "auth.first_admin_created",
                "user",
                userId,
                new { username, shopName },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var session = new UserSession(userId, username, fullName, "role-admin", "Administrator", true, PermissionSet.All);
        _sessions.Set(session);
        _logger.LogInformation("First administrator {Username} created", username);
        return session;
    }

    public async Task<UserSession> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new AuthException(ErrorCodes.AuthInvalid, "Nama pengguna atau kata sandi salah.");
        }

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var user = await connection.QuerySingleOrDefaultAsync<UserRow>(
                new CommandDefinition(
                    """
                    SELECT u.id AS Id, u.username AS Username, u.password_hash AS PasswordHash,
                           u.full_name AS FullName, u.role_id AS RoleId, u.active AS Active,
                           u.failed_login_count AS FailedLoginCount, u.locked_until AS LockedUntil,
                           r.name AS RoleName, r.is_admin AS IsAdmin, r.permissions_json AS PermissionsJson
                    FROM users u
                    JOIN roles r ON r.id = u.role_id
                    WHERE u.username = @username;
                    """,
                    new { username },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (user is null)
        {
            await AuditWriter.WriteAsync(
                connection,
                null,
                null,
                "auth.login_failed",
                "user",
                null,
                new { username },
                now,
                cancellationToken).ConfigureAwait(false);
            throw new AuthException(ErrorCodes.AuthInvalid, "Nama pengguna atau kata sandi salah.");
        }

        if (user.Active == 0)
        {
            await AuditWriter.WriteAsync(
                connection,
                null,
                user.Id,
                "auth.login_inactive",
                "user",
                user.Id,
                new { username },
                now,
                cancellationToken).ConfigureAwait(false);
            throw new AuthException("auth.inactive", "Akun ini dinonaktifkan.");
        }

        var lockedUntil = SqlText.ParseTime(user.LockedUntil);
        if (lockedUntil is not null && lockedUntil > now)
        {
            var remaining = Math.Max(1, (int)Math.Ceiling((lockedUntil.Value - now).TotalMinutes));
            await AuditWriter.WriteAsync(
                connection,
                null,
                user.Id,
                "auth.login_locked",
                "user",
                user.Id,
                new { username },
                now,
                cancellationToken).ConfigureAwait(false);
            throw new AuthException(
                ErrorCodes.AuthLocked,
                $"Terlalu banyak percobaan. Coba lagi dalam {remaining} menit.");
        }

        var verified = await _hasher.VerifyAsync(password, user.PasswordHash, cancellationToken).ConfigureAwait(false);
        if (!verified)
        {
            var failedCount = user.FailedLoginCount + 1;
            var lockUntil = failedCount >= MaxFailedAttempts ? now.Add(LockoutDuration) : (DateTimeOffset?)null;

            await using (var transaction = connection.BeginTransaction(deferred: false))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE users
                    SET failed_login_count = @failedCount, locked_until = @lockedUntil, updated_at = @now
                    WHERE id = @id;
                    """,
                    new
                    {
                        failedCount,
                        lockedUntil = lockUntil is null ? null : SqlText.Now(lockUntil.Value),
                        now = SqlText.Now(now),
                        id = user.Id
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                await AuditWriter.WriteAsync(
                    connection,
                    transaction,
                    user.Id,
                    lockUntil is null ? "auth.login_failed" : "auth.login_locked",
                    "user",
                    user.Id,
                    new { username, failedCount },
                    now,
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (lockUntil is not null)
            {
                throw new AuthException(
                    ErrorCodes.AuthLocked,
                    $"Terlalu banyak percobaan. Coba lagi dalam {LockoutDuration.TotalMinutes:0} menit.");
            }

            throw new AuthException(ErrorCodes.AuthInvalid, "Nama pengguna atau kata sandi salah.");
        }

        await using (var transaction = connection.BeginTransaction(deferred: false))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE users
                SET failed_login_count = 0, locked_until = NULL, last_login_at = @now, updated_at = @now
                WHERE id = @id;
                """,
                new { now = SqlText.Now(now), id = user.Id },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                user.Id,
                "auth.login_succeeded",
                "user",
                user.Id,
                new { username },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var session = new UserSession(
            user.Id,
            user.Username,
            user.FullName,
            user.RoleId,
            user.RoleName,
            user.IsAdmin != 0,
            PermissionSet.FromJson(user.PermissionsJson));

        _sessions.Set(session);
        _logger.LogInformation("User {Username} signed in", user.Username);
        return session;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var session = _sessions.Current;
        _sessions.Clear();
        if (session is null)
        {
            return;
        }

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await AuditWriter.WriteAsync(
            connection,
            null,
            session.UserId,
            "auth.logout",
            "user",
            session.UserId,
            null,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var session = _sessions.Current
            ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");
        PasswordPolicy.Validate(newPassword);

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var currentHash = await connection.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    "SELECT password_hash FROM users WHERE id = @id;",
                    new { id = session.UserId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Pengguna tidak ditemukan.");

        if (!await _hasher.VerifyAsync(currentPassword, currentHash, cancellationToken).ConfigureAwait(false))
        {
            throw new AuthException(ErrorCodes.AuthInvalid, "Kata sandi saat ini salah.");
        }

        var newHash = await _hasher.HashAsync(newPassword, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE users SET password_hash = @hash, updated_at = @now WHERE id = @id;",
                new { hash = newHash, now = SqlText.Now(now), id = session.UserId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "auth.password_changed",
                "user",
                session.UserId,
                null,
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

    private static string ValidateShopName(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is < 2 or > 100)
        {
            throw new ValidationException("company.name_invalid", "Nama toko harus 2–100 karakter.");
        }

        return trimmed;
    }

    private static string ValidateFullName(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is < 2 or > 100)
        {
            throw new ValidationException("user.full_name_invalid", "Nama lengkap harus 2–100 karakter.");
        }

        return trimmed;
    }

    private static string ValidateUsername(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is < 3 or > 32 ||
            !trimmed.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-'))
        {
            throw new ValidationException(
                "user.username_invalid",
                "Nama pengguna 3–32 karakter dan hanya boleh berisi huruf, angka, titik, garis bawah, atau tanda hubung.");
        }

        return trimmed;
    }

    private sealed class CompanyRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LegalName { get; set; }
        public string? TaxNumber { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? LogoPath { get; set; }
        public string Currency { get; set; } = "IDR";
        public int CurrencyMinorUnit { get; set; }
        public string Locale { get; set; } = "id-ID";
        public string Timezone { get; set; } = "Asia/Jakarta";
        public string? ReceiptHeader { get; set; }
        public string? ReceiptFooter { get; set; }
    }

    private sealed class RegisterRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DefaultPriceListId { get; set; }
    }

    private sealed class PriceListRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ApplyPromotions { get; set; }
    }

    private sealed class PaymentTypeRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Shortcut { get; set; }
        public int MarkAsPaid { get; set; }
        public int IsDefault { get; set; }
        public int OpensCashDrawer { get; set; }
    }

    private sealed class SettingRow
    {
        public string Key { get; set; } = string.Empty;
        public string ValueJson { get; set; } = "null";
    }

    private sealed class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public int Active { get; set; }
        public int FailedLoginCount { get; set; }
        public string? LockedUntil { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int IsAdmin { get; set; }
        public string PermissionsJson { get; set; } = "{}";
    }
}
