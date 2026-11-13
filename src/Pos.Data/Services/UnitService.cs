using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class UnitService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public UnitService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<UnitOption>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<UnitRow>(new CommandDefinition(
            "SELECT id AS Id, name AS Name, symbol AS Symbol FROM units ORDER BY name;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new UnitOption(r.Id, r.Name, r.Symbol)).ToList();
    }

    public async Task<IReadOnlyList<UnitOption>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<UnitRow>(new CommandDefinition(
            "SELECT id AS Id, name AS Name, symbol AS Symbol FROM units WHERE active = 1 ORDER BY name;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new UnitOption(r.Id, r.Name, r.Symbol)).ToList();
    }

    public async Task<UnitOption> CreateAsync(
        UnitCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola satuan.");
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
                INSERT INTO units (id, name, symbol, active, created_at, updated_at)
                VALUES (@id, @name, @symbol, 1, @now, @now);
                """,
                new { id, name = request.Name.Trim(), symbol = string.IsNullOrWhiteSpace(request.Symbol) ? null : request.Symbol.Trim(), now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "unit.created",
                "unit",
                id,
                new { name = request.Name.Trim(), symbol = request.Symbol },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("unit.duplicate", "Nama satuan sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new UnitOption(id, request.Name.Trim(), request.Symbol);
    }

    public async Task UpdateAsync(
        string unitId,
        UnitUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CatalogManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola satuan.");
        }

        Validate(request);
        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE units
                SET name = @name, symbol = @symbol, active = @active, updated_at = @now
                WHERE id = @id;
                """,
                new { id = unitId, name = request.Name.Trim(), symbol = string.IsNullOrWhiteSpace(request.Symbol) ? null : request.Symbol.Trim(), active = request.Active ? 1 : 0, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("unit.not_found", "Satuan tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "unit.updated",
                "unit",
                unitId,
                new { name = request.Name.Trim(), symbol = request.Symbol, active = request.Active },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("unit.duplicate", "Nama satuan sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void Validate(UnitCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama satuan wajib diisi.");
        }

        if (request.Name.Trim().Length > 40)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama satuan maksimal 40 karakter.");
        }

        if (request.Symbol is not null && request.Symbol.Trim().Length > 10)
        {
            throw new ValidationException(ErrorCodes.Validation, "Simbol satuan maksimal 10 karakter.");
        }
    }

    private static void Validate(UnitUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama satuan wajib diisi.");
        }

        if (request.Name.Trim().Length > 40)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama satuan maksimal 40 karakter.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class UnitRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
    }
}
