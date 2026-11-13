using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class CashMovementService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CashMovementService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CashMovementSummary>> ListAsync(
        string? registerId = null,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = """
            SELECT
              cm.id AS Id,
              cm.direction AS Direction,
              cm.amount_minor AS AmountMinor,
              cm.reason AS Reason,
              cm.created_at AS CreatedAt,
              u.full_name AS UserName
            FROM cash_movements cm
            JOIN users u ON u.id = cm.user_id
            WHERE (@registerId IS NULL OR cm.register_id = @registerId)
            ORDER BY cm.created_at DESC;
            """;
        var rows = await connection.QueryAsync<CashMovementRow>(new CommandDefinition(
            sql,
            new { registerId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new CashMovementSummary(
            r.Id, r.Direction, r.AmountMinor, r.Reason, r.CreatedAt, r.UserName)).ToList();
    }

    public async Task<CashMovementResult> CreateAsync(
        CashMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola kas.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        var id = Ids.NewId();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Ensure an open business day exists for the register.
            var businessDayId = await EnsureOpenBusinessDayAsync(
                connection, transaction, request.RegisterId, session.UserId, now, cancellationToken).ConfigureAwait(false);

            // Compute current cash balance for the business day.
            var currentBalance = await GetCashBalanceAsync(
                connection, transaction, businessDayId, cancellationToken).ConfigureAwait(false);

            long newBalance;
            if (request.Direction == "in")
            {
                newBalance = checked(currentBalance + request.AmountMinor);
            }
            else if (request.Direction == "out")
            {
                if (request.AmountMinor > currentBalance)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Saldo kas tidak mencukupi untuk pengeluaran.");
                }

                newBalance = checked(currentBalance - request.AmountMinor);
            }
            else
            {
                throw new ValidationException(
                    ErrorCodes.Validation,
                    "Arah kas harus 'in' atau 'out'.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO cash_movements
                  (id, register_id, business_day_id, user_id, direction, amount_minor, reason, created_at)
                VALUES
                  (@id, @registerId, @businessDayId, @userId, @direction, @amountMinor, @reason, @now);
                """,
                new
                {
                    id,
                    registerId = request.RegisterId,
                    businessDayId,
                    userId = session.UserId,
                    direction = request.Direction,
                    amountMinor = request.AmountMinor,
                    reason = request.Reason,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "cash.movement",
                "cash_movement",
                id,
                new { direction = request.Direction, amount = request.AmountMinor, reason = request.Reason },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new CashMovementResult(id, request.Direction, request.AmountMinor, newBalance, SqlText.Now(now));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> EnsureOpenBusinessDayAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string registerId,
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var open = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT id FROM business_days
            WHERE register_id = @registerId AND status = 'open'
            ORDER BY opened_at DESC
            LIMIT 1;
            """,
            new { registerId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (open is not null)
        {
            return open;
        }

        var zone = ResolveTimezone(null);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var dateKey = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var newId = Ids.NewId();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO business_days
              (id, register_id, business_date, status, opened_at, opened_by,
               opening_cash_minor, created_at, updated_at)
            VALUES
              (@id, @registerId, @date, 'open', @now, @userId, 0, @now, @now);
            """,
            new { id = newId, registerId, date = dateKey, now = SqlText.Now(now), userId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return newId;
    }

    private async Task<long> GetCashBalanceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string businessDayId,
        CancellationToken cancellationToken)
    {
        var salesCash = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(p.amount_minor), 0)
            FROM payments p
            JOIN sales s ON s.id = p.sale_id
            JOIN payment_types pt ON pt.id = p.payment_type_id
            WHERE s.business_day_id = @businessDayId
              AND p.status = 'captured'
              AND pt.opens_cash_drawer = 1;
            """,
            new { businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var cashMovements = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(CASE WHEN direction = 'in' THEN amount_minor ELSE -amount_minor END), 0)
            FROM cash_movements
            WHERE business_day_id = @businessDayId;
            """,
            new { businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return checked(salesCash + cashMovements);
    }

    private static void Validate(CashMovementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RegisterId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Register wajib dipilih.");
        }

        if (request.AmountMinor <= 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Jumlah harus lebih dari nol.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(ErrorCodes.Validation, "Alasan wajib diisi.");
        }

        if (request.Reason.Trim().Length > 200)
        {
            throw new ValidationException(ErrorCodes.Validation, "Alasan maksimal 200 karakter.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static TimeZoneInfo ResolveTimezone(string? timezone) => TimeZoneInfo.Utc;

    private sealed class CashMovementRow
    {
        public string Id { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public long AmountMinor { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}
