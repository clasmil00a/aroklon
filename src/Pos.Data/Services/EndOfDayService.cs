using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class EndOfDayService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public EndOfDayService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<EndOfDayResult> CloseAsync(
        EndOfDayRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PosCloseDay))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk menutup hari.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Find the open business day for the register.
            var day = await connection.QuerySingleOrDefaultAsync<DayRow>(new CommandDefinition(
                """
                SELECT id AS Id, business_date AS BusinessDate, opening_cash_minor AS OpeningCashMinor
                FROM business_days
                WHERE register_id = @registerId AND status = 'open'
                ORDER BY opened_at DESC
                LIMIT 1;
                """,
                new { registerId = request.RegisterId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)
                ?? throw new NotFoundException(
                    "business_day.not_open",
                    "Tidak ada hari bisnis yang terbuka untuk register ini.");

            // Compute expected cash: opening + sales cash + cash movements.
            var expectedCash = await GetExpectedCashAsync(
                connection, transaction, day.Id, cancellationToken).ConfigureAwait(false);

            var closingCash = request.ClosingCashMinor;
            var variance = checked(closingCash - expectedCash);

            // Generate report number.
            var register = await GetRegisterAsync(
                connection, transaction, request.RegisterId, cancellationToken).ConfigureAwait(false);
            var reportNumber = await NextNumberAsync(
                connection, transaction, "report", register.Id, "Z-", now, cancellationToken).ConfigureAwait(false);

            // Aggregate totals for the business day.
            var totals = await GetDayTotalsAsync(
                connection, transaction, day.Id, cancellationToken).ConfigureAwait(false);

            // Close the business day.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE business_days
                SET status = 'closed', closed_at = @now, closed_by = @userId,
                    report_number = @reportNumber,
                    closing_cash_minor = @closingCash,
                    expected_cash_minor = @expectedCash,
                    variance_minor = @variance,
                    updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    now = SqlText.Now(now),
                    userId = session.UserId,
                    reportNumber,
                    closingCash,
                    expectedCash,
                    variance,
                    id = day.Id
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "day.closed",
                "business_day",
                day.Id,
                new {
                    reportNumber, businessDate = day.BusinessDate, closingCash,
                    expectedCash, variance, salesCount = totals.SalesCount,
                    grandTotal = totals.GrandTotal
                },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new EndOfDayResult(
                reportNumber,
                day.BusinessDate,
                totals.SalesCount,
                totals.RefundCount,
                totals.TaxableTotal,
                totals.TaxTotal,
                totals.DiscountTotal,
                totals.GrandTotal,
                totals.CashIn,
                totals.CashOut,
                closingCash,
                expectedCash,
                variance);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<EndOfDayResult?> GetOpenDaySummaryAsync(
        string registerId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var day = await connection.QuerySingleOrDefaultAsync<DayRow>(new CommandDefinition(
            """
            SELECT id AS Id, business_date AS BusinessDate, opening_cash_minor AS OpeningCashMinor
            FROM business_days
            WHERE register_id = @registerId AND status = 'open'
            ORDER BY opened_at DESC
            LIMIT 1;
            """,
            new { registerId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (day is null)
        {
            return null;
        }

        var expectedCash = await GetExpectedCashAsync(
            connection, null, day.Id, cancellationToken).ConfigureAwait(false);
        var totals = await GetDayTotalsAsync(
            connection, null, day.Id, cancellationToken).ConfigureAwait(false);

        return new EndOfDayResult(
            string.Empty, day.BusinessDate, totals.SalesCount, totals.RefundCount,
            totals.TaxableTotal, totals.TaxTotal, totals.DiscountTotal,
            totals.GrandTotal, totals.CashIn, totals.CashOut,
            0, expectedCash, checked(0 - expectedCash));
    }

    public async Task<IReadOnlyList<EndOfDayResult>> ListClosedDaysAsync(
        string? registerId = null,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ClosedDayRow>(new CommandDefinition(
            """
            SELECT
              bd.report_number AS ReportNumber,
              bd.business_date AS BusinessDate,
              bd.sales_count AS SalesCount,
              bd.refund_count AS RefundCount,
              bd.taxable_total_minor AS TaxableTotal,
              bd.tax_total_minor AS TaxTotal,
              bd.discount_total_minor AS DiscountTotal,
              bd.grand_total_minor AS GrandTotal,
              bd.cash_in_total_minor AS CashIn,
              bd.cash_out_total_minor AS CashOut,
              bd.closing_cash_minor AS ClosingCash,
              bd.expected_cash_minor AS ExpectedCash,
              bd.variance_minor AS Variance
            FROM business_days bd
            WHERE bd.status = 'closed'
              AND (@registerId IS NULL OR bd.register_id = @registerId)
            ORDER BY bd.closed_at DESC;
            """,
            new { registerId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new EndOfDayResult(
            r.ReportNumber ?? string.Empty,
            r.BusinessDate,
            r.SalesCount,
            r.RefundCount,
            r.TaxableTotal,
            r.TaxTotal,
            r.DiscountTotal,
            r.GrandTotal,
            r.CashIn,
            r.CashOut,
            r.ClosingCash ?? 0,
            r.ExpectedCash ?? 0,
            r.Variance ?? 0)).ToList();
    }

    private async Task<long> GetExpectedCashAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string businessDayId,
        CancellationToken cancellationToken)
    {
        var openingCash = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT opening_cash_minor FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

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

        return checked(openingCash + salesCash + cashMovements);
    }

    private async Task<DayTotals> GetDayTotalsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string businessDayId,
        CancellationToken cancellationToken)
    {
        var salesCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT sales_count FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var refundCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT refund_count FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var taxableTotal = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT taxable_total_minor FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var taxTotal = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT tax_total_minor FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var discountTotal = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT discount_total_minor FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var grandTotal = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT grand_total_minor FROM business_days WHERE id = @id;",
            new { id = businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var cashIn = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(CASE WHEN direction = 'in' THEN amount_minor ELSE 0 END), 0)
            FROM cash_movements
            WHERE business_day_id = @businessDayId;
            """,
            new { businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var cashOut = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(CASE WHEN direction = 'out' THEN amount_minor ELSE 0 END), 0)
            FROM cash_movements
            WHERE business_day_id = @businessDayId;
            """,
            new { businessDayId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new DayTotals(
            salesCount, refundCount, taxableTotal, taxTotal,
            discountTotal, grandTotal, cashIn, cashOut);
    }

    private static void Validate(EndOfDayRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RegisterId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Register wajib dipilih.");
        }

        if (request.ClosingCashMinor < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Tutup kas tidak boleh negatif.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private async Task<RegisterRow> GetRegisterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string registerId,
        CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<RegisterRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name
            FROM registers WHERE id = @registerId AND active = 1;
            """,
            new { registerId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false)
        ?? throw new PosException(ErrorCodes.Database, "Register tidak ditemukan.");

    private async Task<string> NextNumberAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string type,
        string registerId,
        string prefix,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var localDate = DateOnly.FromDateTime(now.UtcDateTime);
        var periodKey = localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var sequence = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO document_sequences
              (id, document_type, register_id, period_key, prefix, next_number,
               reset_on_day_close, created_at, updated_at)
            VALUES
              (@id, @type, @registerId, @periodKey, @prefix, 2, 1, @now, @now)
            ON CONFLICT(document_type, register_id, period_key) DO UPDATE SET
              next_number = next_number + 1,
              updated_at = excluded.updated_at
            RETURNING next_number - 1;
            """,
            new
            {
                id = Ids.NewId(),
                type,
                registerId,
                periodKey,
                prefix,
                now = SqlText.Now(now)
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return prefix + periodKey + "-" + sequence.ToString("0000", CultureInfo.InvariantCulture);
    }

    private sealed record DayTotals(
        long SalesCount,
        long RefundCount,
        long TaxableTotal,
        long TaxTotal,
        long DiscountTotal,
        long GrandTotal,
        long CashIn,
        long CashOut);

    private sealed class DayRow
    {
        public string Id { get; set; } = string.Empty;
        public string BusinessDate { get; set; } = string.Empty;
        public long OpeningCashMinor { get; set; }
    }

    private sealed class RegisterRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ClosedDayRow
    {
        public string? ReportNumber { get; set; }
        public string BusinessDate { get; set; } = string.Empty;
        public long SalesCount { get; set; }
        public long RefundCount { get; set; }
        public long TaxableTotal { get; set; }
        public long TaxTotal { get; set; }
        public long DiscountTotal { get; set; }
        public long GrandTotal { get; set; }
        public long CashIn { get; set; }
        public long CashOut { get; set; }
        public long? ClosingCash { get; set; }
        public long? ExpectedCash { get; set; }
        public long? Variance { get; set; }
    }
}
