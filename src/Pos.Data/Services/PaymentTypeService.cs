using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class PaymentTypeService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public PaymentTypeService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PaymentTypeSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<PaymentTypeRow>(new CommandDefinition(
            """
            SELECT
              id AS Id,
              name AS Name,
              icon AS Icon,
              shortcut AS Shortcut,
              mark_as_paid AS MarkAsPaid,
              is_default AS IsDefault,
              opens_cash_drawer AS OpensCashDrawer,
              active AS Active,
              sort_order AS SortOrder
            FROM payment_types
            ORDER BY sort_order, name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new PaymentTypeSummary(
            r.Id, r.Name, r.Icon, r.Shortcut, r.MarkAsPaid, r.IsDefault,
            r.OpensCashDrawer, r.Active, r.SortOrder)).ToList();
    }

    public async Task<PaymentTypeSummary> CreateAsync(
        PaymentTypeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola tipe pembayaran.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        var id = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Jika jadi default, unset default yang ada dulu (hanya satu default diperbolehkan)
            if (request.IsDefault)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE payment_types SET is_default = 0 WHERE is_default = 1;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO payment_types
                  (id, name, icon, shortcut, mark_as_paid, is_default,
                   opens_cash_drawer, active, sort_order, created_at, updated_at)
                VALUES
                  (@id, @name, @icon, @shortcut, @markAsPaid, @isDefault,
                   @opensCashDrawer, 1, @sortOrder, @now, @now);
                """,
                new
                {
                    id,
                    name = request.Name.Trim(),
                    icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
                    shortcut = string.IsNullOrWhiteSpace(request.Shortcut) ? null : request.Shortcut.Trim(),
                    markAsPaid = request.MarkAsPaid ? 1 : 0,
                    isDefault = request.IsDefault ? 1 : 0,
                    opensCashDrawer = request.OpensCashDrawer ? 1 : 0,
                    sortOrder = request.SortOrder,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "payment_type.created",
                "payment_type",
                id,
                new { name = request.Name.Trim(), markAsPaid = request.MarkAsPaid },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("payment_type.duplicate", "Nama tipe pembayaran sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new PaymentTypeSummary(
            id, request.Name.Trim(), request.Icon, request.Shortcut,
            request.MarkAsPaid, request.IsDefault, request.OpensCashDrawer, true,
            request.SortOrder);
    }

    public async Task UpdateAsync(
        string paymentTypeId,
        PaymentTypeUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola tipe pembayaran.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<PaymentTypeRow>(new CommandDefinition(
                """
                SELECT id AS Id, name AS Name, icon AS Icon, shortcut AS Shortcut,
                       mark_as_paid AS MarkAsPaid, is_default AS IsDefault,
                       opens_cash_drawer AS OpensCashDrawer, active AS Active,
                       sort_order AS SortOrder
                FROM payment_types WHERE id = @id;
                """,
                new { id = paymentTypeId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)
                ?? throw new NotFoundException("payment_type.not_found", "Tipe pembayaran tidak ditemukan.");

            // Jika jadi default, unset default yang ada dulu
            if (request.IsDefault == true)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE payment_types SET is_default = 0 WHERE is_default = 1;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE payment_types
                SET name = COALESCE(@name, name),
                    icon = COALESCE(@icon, icon),
                    shortcut = COALESCE(@shortcut, shortcut),
                    mark_as_paid = COALESCE(@markAsPaid, mark_as_paid),
                    is_default = COALESCE(@isDefault, is_default),
                    opens_cash_drawer = COALESCE(@opensCashDrawer, opens_cash_drawer),
                    active = COALESCE(@active, active),
                    sort_order = COALESCE(@sortOrder, sort_order),
                    updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    id = paymentTypeId,
                    name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                    icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
                    shortcut = string.IsNullOrWhiteSpace(request.Shortcut) ? null : request.Shortcut.Trim(),
                    markAsPaid = request.MarkAsPaid,
                    isDefault = request.IsDefault,
                    opensCashDrawer = request.OpensCashDrawer,
                    active = request.Active,
                    sortOrder = request.SortOrder,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("payment_type.not_found", "Tipe pembayaran tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "payment_type.updated",
                "payment_type",
                paymentTypeId,
                new { name = request.Name, active = request.Active },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException("payment_type.duplicate", "Nama tipe pembayaran sudah dipakai.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void Validate(PaymentTypeCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 1)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama tipe pembayaran wajib diisi.");
        }

        if (request.Name.Trim().Length > 60)
        {
            throw new ValidationException(ErrorCodes.Validation, "Nama tipe pembayaran maksimal 60 karakter.");
        }

        if (request.Shortcut is not null && request.Shortcut.Trim().Length > 10)
        {
            throw new ValidationException(ErrorCodes.Validation, "Pintasan maksimal 10 karakter.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class PaymentTypeRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Shortcut { get; set; }
        public bool MarkAsPaid { get; set; }
        public bool IsDefault { get; set; }
        public bool OpensCashDrawer { get; set; }
        public bool Active { get; set; }
        public int SortOrder { get; set; }
    }
}
