using Dapper;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;

namespace Pos.Data.Services;

public sealed class AdminUserService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;

    public AdminUserService(IDbConnectionFactory factory, ISessionStore sessions)
    {
        _factory = factory;
        _sessions = sessions;
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
}
