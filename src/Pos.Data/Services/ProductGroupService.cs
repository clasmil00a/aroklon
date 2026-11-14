using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class ProductGroupService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public ProductGroupService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProductGroup>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ProductGroupRow>(new CommandDefinition(
            """
            SELECT
              id AS Id,
              name AS Name,
              description AS Description,
              sort_order AS SortOrder,
              active AS Active
            FROM product_groups
            ORDER BY sort_order, name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new ProductGroup(
            r.Id, r.Name, r.Description, r.SortOrder, r.Active)).ToList();
    }

    public async Task<IReadOnlyList<ProductGroup>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ProductGroupRow>(new CommandDefinition(
            """
            SELECT
              id AS Id,
              name AS Name,
              description AS Description,
              sort_order AS SortOrder,
              active AS Active
            FROM product_groups
            WHERE active = 1
            ORDER BY sort_order, name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new ProductGroup(
            r.Id, r.Name, r.Description, r.SortOrder, r.Active)).ToList();
    }

    public async Task<ProductGroup> CreateAsync(
        ProductGroupCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola grup produk.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        var id = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_groups
                  (id, name, description, sort_order, active, created_at, updated_at)
                VALUES
                  (@id, @name, @description, @sort_order, 1, @now, @now);
                """,
                new
                {
                    id,
                    name = request.Name.Trim(),
                    description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    sort_order = request.SortOrder,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "product_group.created",
                "product_group",
                id,
                new { name = request.Name.Trim(), sort_order = request.SortOrder },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("product_group.duplicate", "Nama grup produk sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new ProductGroup(id, request.Name.Trim(), request.Description.Trim(), request.SortOrder, true);
    }

    public async Task UpdateAsync(
        string groupId,
        ProductGroupUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola grup produk.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE product_groups
                SET name = COALESCE(@name, name),
                    description = COALESCE(@description, description),
                    sort_order = COALESCE(@sort_order, sort_order),
                    active = COALESCE(@active, active),
                    updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    id = groupId,
                    name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                    description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    sort_order = request.SortOrder,
                    active = request.Active,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("product_group.not_found", "Grup produk tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "product_group.updated",
                "product_group",
                groupId,
                new { name = request.Name, active = request.Active },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("product_group.duplicate", "Nama grup produk sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void Validate(ProductGroupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama grup produk wajib diisi.");
        }

        if (request.Name.Trim().Length > 60)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama grup produk maksimal 60 karakter.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class ProductGroupRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool Active { get; set; }
    }
}
