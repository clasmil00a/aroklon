using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class AdminUserService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public AdminUserService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IPasswordHasher hasher,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<IReadOnlyList<UserInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var session = _sessions.Current
            ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");
        if (!session.Can(PermissionKeys.UsersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk melihat daftar pengguna.");
        }

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<UserRow>(
                new CommandDefinition(
                    """
                    SELECT u.id AS Id, u.username AS Username, u.full_name AS FullName,
                           u.role_id AS RoleId, r.name AS RoleName, u.active AS Active,
                           u.last_login_at AS LastLoginAt
                    FROM users u
                    JOIN roles r ON r.id = u.role_id
                    ORDER BY u.active DESC, u.username;
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(row => new UserInfo(
            row.Id,
            row.Username,
            row.FullName,
            row.RoleId,
            row.RoleName,
            row.Active != 0,
            row.LastLoginAt)).ToList();
    }

    public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var session = _sessions.Current
            ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");
        if (!session.Can(PermissionKeys.UsersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk melihat daftar role.");
        }

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RoleRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, name AS Name, is_admin AS IsAdmin,
                           permissions_json AS PermissionsJson
                    FROM roles
                    ORDER BY name;
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(row => new RoleSummary(
            row.Id, row.Name, row.IsAdmin != 0, row.PermissionsJson)).ToList();
    }

    public async Task<UserDetail> CreateAsync(UserCreateRequest request, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.UsersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk membuat pengguna.");
        }

        ValidateCreate(request);
        var now = _clock.UtcNow;
        var id = Ids.NewId();
        var hash = await _hasher.HashAsync(request.Password);

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Verify role exists
            var roleExists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COUNT(*) FROM roles WHERE id = @roleId;",
                new { roleId = request.RoleId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (roleExists == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("role.not_found", "Role tidak ditemukan.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO users
                  (id, username, password_hash, full_name, role_id, active, created_at, updated_at)
                VALUES
                  (@id, @username, @hash, @fullName, @roleId, 1, @now, @now);
                """,
                new
                {
                    id,
                    username = request.Username.Trim(),
                    hash,
                    fullName = request.FullName.Trim(),
                    roleId = request.RoleId,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "user.created",
                "user",
                id,
                new { username = request.Username.Trim(), roleId = request.RoleId },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("user.duplicate", "Nama pengguna sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(string userId, UserUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.UsersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengubah pengguna.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
                """
                SELECT id AS Id, username AS Username, full_name AS FullName,
                       role_id AS RoleId, active AS Active, last_login_at AS LastLoginAt
                FROM users WHERE id = @id;
                """,
                new { id = userId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)
                ?? throw new NotFoundException("user.not_found", "Pengguna tidak ditemukan.");

            if (request.NewPassword is not null)
            {
                if (request.NewPassword.Length < 8)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw new ValidationException(ErrorCodes.Validation, "Kata sandi minimal 8 karakter.");
                }

                var hash = await _hasher.HashAsync(request.NewPassword);
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE users SET password_hash = @hash, updated_at = @now WHERE id = @id;",
                    new { hash, id = userId, now = SqlText.Now(now) },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE users
                SET full_name = COALESCE(@fullName, full_name),
                    role_id = COALESCE(@roleId, role_id),
                    active = COALESCE(@active, active),
                    updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    id = userId,
                    fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
                    roleId = request.RoleId,
                    active = request.Active,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("user.not_found", "Pengguna tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "user.updated",
                "user",
                userId,
                new { fullName = request.FullName, roleId = request.RoleId, active = request.Active },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("user.duplicate", "Nama pengguna sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task ToggleActiveAsync(string userId, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.UsersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengubah status pengguna.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT active FROM users WHERE id = @id;",
                new { id = userId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (existing == null)
            {
                throw new NotFoundException("user.not_found", "Pengguna tidak ditemukan.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE users SET active = @active, updated_at = @now WHERE id = @id;",
                new { active = existing == 1 ? 0 : 1, id = userId, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "user.toggled",
                "user",
                userId,
                new { active = existing == 1 ? 0 : 1 },
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

    public async Task<UserDetail> GetDetailAsync(string userId, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<UserDetailRow>(new CommandDefinition(
            """
            SELECT u.id AS Id, u.username AS Username, u.full_name AS FullName,
                   u.role_id AS RoleId, r.name AS RoleName, u.active AS Active,
                   u.last_login_at AS LastLoginAt, r.permissions_json AS PermissionsJson
            FROM users u
            JOIN roles r ON r.id = u.role_id
            WHERE u.id = @id;
            """,
            new { id = userId },
            cancellationToken: cancellationToken)).ConfigureAwait(false)
            ?? throw new NotFoundException("user.not_found", "Pengguna tidak ditemukan.");

        return new UserDetail(
            row.Id, row.Username, row.FullName, row.RoleId, row.RoleName,
            row.Active != 0, row.LastLoginAt, row.PermissionsJson);
    }

    private static void ValidateCreate(UserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 2)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama pengguna minimal 2 karakter.");
        }

        if (request.Username.Trim().Length > 40)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama pengguna maksimal 40 karakter.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama lengkap minimal 2 karakter.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException(ErrorCodes.Validation, "Kata sandi minimal 8 karakter.");
        }

        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Role wajib dipilih.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int Active { get; set; }
        public string? LastLoginAt { get; set; }
    }

    private sealed class RoleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int IsAdmin { get; set; }
        public string PermissionsJson { get; set; } = string.Empty;
    }

    private sealed class UserDetailRow
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int Active { get; set; }
        public string? LastLoginAt { get; set; }
        public string PermissionsJson { get; set; } = string.Empty;
    }
}
