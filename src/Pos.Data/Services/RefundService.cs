using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Pos.Domain.Primitives;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class RefundService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;
    private readonly SaleService _saleService;

    public RefundService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock,
        SaleService saleService)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
        _saleService = saleService;
    }

    public async Task<RefundResult> CreateAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PosRefund))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk refund.");
        }

        Validate(request);
        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // Load original sale.
            var original = await connection.QuerySingleOrDefaultAsync<SaleRow>(new CommandDefinition(
                """
                SELECT
                  s.id AS Id,
                  s.number AS Number,
                  s.status AS Status,
                  s.total_minor AS TotalMinor,
                  s.register_id AS RegisterId,
                  s.business_day_id AS BusinessDayId,
                  s.user_id AS UserId,
                  s.customer_id AS CustomerId,
                  s.customer_name_snapshot AS CustomerName,
                  s.price_list_id AS PriceListId,
                  s.currency AS Currency,
                  s.currency_minor_unit AS CurrencyMinorUnit
                FROM sales s
                WHERE s.id = @saleId;
                """,
                new { saleId = request.SaleId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)
                ?? throw new NotFoundException("sale.not_found", "Transaksi asli tidak ditemukan.");

            if (original.Status == "refunded")
            {
                throw new ValidationException(
                    ErrorCodes.Validation,
                    "Transaksi ini sudah direfund sepenuhnya.");
            }

            if (original.Status == "voided")
            {
                throw new ValidationException(
                    ErrorCodes.Validation,
                    "Transaksi yang dibatalkan tidak dapat direfund.");
            }

            // Load original sale items (non-voided, with remaining refundable qty).
            var originalItems = await connection.QueryAsync<SaleItemRow>(new CommandDefinition(
                """
                SELECT
                  si.id AS Id,
                  si.product_id AS ProductId,
                  si.product_unit_id AS ProductUnitId,
                  si.name_snapshot AS Name,
                  si.unit_name_snapshot AS UnitName,
                  si.unit_factor_milli AS UnitFactorMilli,
                  si.qty_milli AS QtyMilli,
                  si.unit_price_minor AS UnitPriceMinor,
                  si.discount_total_minor AS DiscountMinor,
                  si.line_total_minor AS LineTotalMinor,
                  si.refunded_qty_milli AS RefundedQtyMilli,
                  si.status AS Status,
                  COALESCE((
                    SELECT tr.rate_bp
                    FROM product_taxes pt
                    JOIN tax_rates tr ON tr.id = pt.tax_rate_id AND tr.active = 1
                    WHERE pt.product_id = si.product_id
                    ORDER BY tr.rate_bp DESC
                    LIMIT 1
                  ), 0) AS RateBp,
                  COALESCE((
                    SELECT pt.tax_rate_id
                    FROM product_taxes pt
                    JOIN tax_rates tr ON tr.id = pt.tax_rate_id AND tr.active = 1
                    WHERE pt.product_id = si.product_id
                    ORDER BY tr.rate_bp DESC
                    LIMIT 1
                  ), '') AS TaxRateId
                FROM sale_items si
                WHERE si.sale_id = @saleId AND si.status <> 'voided';
                """,
                new { saleId = request.SaleId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Validate refund lines and compute totals.
            var refundLines = new List<ResolvedRefundLine>();
            long refundSubtotal = 0;
            long refundDiscount = 0;
            long refundTax = 0;

            foreach (var input in request.Lines)
            {
                if (input.RefundQtyMilli <= 0)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Jumlah refund setiap baris harus lebih dari nol.");
                }

                var item = originalItems.FirstOrDefault(i => i.Id == input.SaleItemId)
                    ?? throw new NotFoundException(
                        "sale_item.not_found",
                        "Baris transaksi asli tidak ditemukan.");

                if (item.Status == "voided")
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Baris " + item.Name + " sudah dibatalkan dan tidak dapat direfund.");
                }

                var refundable = item.QtyMilli - item.RefundedQtyMilli;
                if (input.RefundQtyMilli > refundable)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Jumlah refund melebihi sisa yang dapat direfund untuk " + item.Name + ".");
                }

                var lineUnitPrice = item.UnitPriceMinor;
                var lineGross = MoneyMath.LineTotal(input.RefundQtyMilli, lineUnitPrice);
                var lineDiscount = item.DiscountMinor > 0
                    ? CartMath.DiscountFor(lineGross, DiscountKind.Percent,
                        (int)(item.DiscountMinor * 10000 / item.QtyMilli))
                    : 0;
                var lineTax = item.RateBp > 0
                    ? MoneyMath.PercentOf(checked(lineGross - lineDiscount), item.RateBp)
                    : 0;

                refundSubtotal = checked(refundSubtotal + lineGross);
                refundDiscount = checked(refundDiscount + lineDiscount);
                refundTax = checked(refundTax + lineTax);

                refundLines.Add(new ResolvedRefundLine(
                    item, input.RefundQtyMilli, lineUnitPrice, lineDiscount, lineTax,
                    checked(lineGross - lineDiscount + lineTax)));
            }

            long refundTotal = checked(refundSubtotal - refundDiscount + refundTax);

            // Validate payment.
            var paymentType = await GetPaymentTypeAsync(
                connection, transaction, request.PaymentTypeId, cancellationToken).ConfigureAwait(false);

            long paid;
            long change;
            if (paymentType.MarkAsPaid)
            {
                if (request.TenderedMinor < refundTotal)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Jumlah pembayaran refund kurang dari total refund.");
                }

                paid = refundTotal;
                change = request.TenderedMinor - refundTotal;
            }
            else
            {
                // Credit refund: customer gets credit, no immediate cash.
                paid = 0;
                change = 0;
            }

            // Determine new status of original sale.
            var alreadyRefunded = await GetAlreadyRefundedQtyAsync(
                connection, transaction, request.SaleId, cancellationToken).ConfigureAwait(false);

            var totalRefundableQty = originalItems.Sum(i => i.QtyMilli - i.RefundedQtyMilli);
            var refundingQty = request.Lines.Sum(l => l.RefundQtyMilli);
            var newTotalRefunded = checked(alreadyRefunded + refundingQty);

            string originalNewStatus;
            if (newTotalRefunded >= originalItems.Sum(i => i.QtyMilli))
            {
                originalNewStatus = "refunded";
            }
            else
            {
                originalNewStatus = "partially_refunded";
            }

            // Create refund sale.
            var register = await GetRegisterAsync(
                connection, transaction, original.RegisterId, cancellationToken).ConfigureAwait(false);
            var businessDayId = original.BusinessDayId;
            var refundNumber = await NextNumberAsync(
                connection, transaction, "refund", register.Id, "REF-", now, cancellationToken).ConfigureAwait(false);

            var refundSaleId = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO sales
                  (id, number, type, status, parent_sale_id, register_id, business_day_id,
                   user_id, customer_id, customer_name_snapshot, price_list_id,
                   currency, currency_minor_unit, subtotal_minor,
                   discount_kind, discount_value, discount_total_minor, tax_total_minor,
                   total_minor, paid_total_minor, change_minor, balance_minor, note,
                   created_at, updated_at, closed_at)
                VALUES
                  (@id, @number, 'refund', 'closed', @parentSaleId, @registerId, @businessDayId,
                   @userId, @customerId, @customerName, @priceListId,
                   @currency, @currencyMinorUnit, @subtotal,
                   'none', 0, @discount, @tax, @total, @paid, @change, 0, @note,
                   @now, @now, @now);
                """,
                new
                {
                    id = refundSaleId,
                    number = refundNumber,
                    parentSaleId = request.SaleId,
                    registerId = register.Id,
                    businessDayId,
                    userId = session.UserId,
                    customerId = original.CustomerId,
                    customerName = original.CustomerName,
                    priceListId = original.PriceListId,
                    currency = original.Currency,
                    currencyMinorUnit = original.CurrencyMinorUnit,
                    subtotal = refundSubtotal,
                    discount = refundDiscount,
                    tax = refundTax,
                    total = refundTotal,
                    paid,
                    change,
                    note = EmptyToNull(request.Reason),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Insert refund sale items and reverse stock.
            var sort = 0;
            foreach (var line in refundLines)
            {
                var saleItemId = Ids.NewId();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO sale_items
                      (id, sale_id, product_id, product_unit_id, name_snapshot, code_snapshot,
                       unit_name_snapshot, unit_factor_milli, qty_milli, base_qty_milli,
                       unit_price_minor, unit_cost_minor, discount_kind, discount_value,
                       discount_total_minor, tax_total_minor, line_total_minor, status,
                       comment, sort_order, created_at, updated_at)
                    VALUES
                      (@id, @saleId, @productId, @productUnitId, @name, NULL,
                       @unitName, @factor, @qty, @baseQty,
                       @unitPrice, 0, 'none', 0, @discount, @tax, @lineTotal, 'confirmed',
                       @comment, @sort, @now, @now);
                    """,
                    new
                    {
                        id = saleItemId,
                        saleId = refundSaleId,
                        productId = line.Item.ProductId,
                        productUnitId = line.Item.ProductUnitId,
                        name = line.Item.Name + " (REFUND)",
                        unitName = line.Item.UnitName,
                        factor = line.Item.UnitFactorMilli,
                        qty = line.RefundQtyMilli,
                        baseQty = UnitMath.ToBase(line.RefundQtyMilli, line.Item.UnitFactorMilli),
                        unitPrice = line.UnitPriceMinor,
                        discount = line.DiscountMinor,
                        tax = line.TaxMinor,
                        lineTotal = line.TotalMinor,
                        comment = EmptyToNull(line.Item.Name + " - Refund"),
                        sort = sort++,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Insert per-line tax for the refund.
                if (line.Item.TaxRateId is not null && line.Item.TaxRateId.Length > 0 && line.TaxMinor > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO sale_item_taxes
                          (id, sale_item_id, tax_rate_id, name_snapshot, rate_bp,
                           taxable_base_minor, amount_minor, created_at)
                        VALUES
                          (@id, @saleItemId, @taxRateId, @name, @rateBp,
                           @taxableBase, @taxAmount, @now);
                        """,
                        new
                        {
                            id = Ids.NewId(),
                            saleItemId,
                            taxRateId = line.Item.TaxRateId,
                            name = line.Item.Name,
                            rateBp = line.Item.RateBp,
                            taxableBase = checked(line.UnitPriceMinor * line.RefundQtyMilli / 1000 - line.DiscountMinor),
                            taxAmount = line.TaxMinor,
                            now = SqlText.Now(now)
                        },
                        transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                }

                // Reverse stock: refund adds stock back.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO stock_movements
                      (id, product_id, type, qty_delta_base_milli, unit_cost_minor,
                       reference_type, reference_id, note, user_id, created_at)
                    VALUES
                      (@id, @productId, 'refund', @qty, 0,
                       'refund', @refundSaleId, @note, @userId, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        productId = line.Item.ProductId,
                        qty = UnitMath.ToBase(line.RefundQtyMilli, line.Item.UnitFactorMilli),
                        note = line.Item.Name + " refund",
                        refundSaleId,
                        userId = session.UserId,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Update original sale item refunded_qty_milli.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE sale_items
                    SET refunded_qty_milli = refunded_qty_milli + @qty,
                        updated_at = @now
                    WHERE id = @itemId;
                    """,
                    new { qty = line.RefundQtyMilli, itemId = line.Item.Id, now = SqlText.Now(now) },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            // Update original sale status.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sales
                SET status = @status, updated_at = @now
                WHERE id = @saleId;
                """,
                new { status = originalNewStatus, saleId = request.SaleId, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Insert refund payment.
            if (paid > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO payments
                      (id, sale_id, payment_type_id, kind, amount_minor, tendered_minor,
                       change_minor, status, created_by, created_at, updated_at)
                    VALUES
                      (@id, @saleId, @paymentTypeId, 'refund', @amount, @tendered,
                       @change, 'captured', @userId, @now, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        saleId = refundSaleId,
                        paymentTypeId = request.PaymentTypeId,
                        amount = paid,
                        tendered = request.TenderedMinor,
                        change,
                        userId = session.UserId,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            // Update business day refund totals.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE business_days
                SET refund_count = refund_count + 1,
                    refund_total_minor = refund_total_minor + @refundTotal,
                    updated_at = @now
                WHERE id = @businessDayId;
                """,
                new { refundTotal, businessDayId, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "sale.refunded",
                "sale",
                refundSaleId,
                new { originalSale = original.Number, refundNumber, total = refundTotal },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new RefundResult(
                refundSaleId, refundNumber, "closed", refundTotal, paid, change);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<RefundSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RefundRow>(new CommandDefinition(
            """
            SELECT
              s.id AS Id,
              s.number AS Number,
              s.created_at AS CreatedAt,
              ps.number AS OriginalSaleNumber,
              s.total_minor AS RefundTotalMinor,
              u.full_name AS UserName
            FROM sales s
            JOIN sales ps ON ps.id = s.parent_sale_id
            JOIN users u ON u.id = s.user_id
            WHERE s.type = 'refund'
            ORDER BY s.created_at DESC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new RefundSummary(
            r.Id, r.Number, r.CreatedAt, r.OriginalSaleNumber,
            Math.Abs(r.RefundTotalMinor), r.UserName)).ToList();
    }

    private async Task<long> GetAlreadyRefundedQtyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string saleId,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(refunded_qty_milli), 0)
            FROM sale_items
            WHERE sale_id = @saleId;
            """,
            new { saleId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static void Validate(RefundRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SaleId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Transaksi asli wajib dipilih.");
        }

        if (request.Lines.Count == 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Minimal satu baris harus direfund.");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentTypeId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Tipe pembayaran refund wajib dipilih.");
        }

        if (request.TenderedMinor < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Jumlah pembayaran tidak boleh negatif.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(ErrorCodes.Validation, "Alasan refund wajib diisi.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private async Task<PaymentTypeRow> GetPaymentTypeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string paymentTypeId,
        CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<PaymentTypeRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, mark_as_paid AS MarkAsPaid,
                   opens_cash_drawer AS OpensCashDrawer
            FROM payment_types WHERE id = @paymentTypeId AND active = 1;
            """,
            new { paymentTypeId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false)
        ?? throw new NotFoundException("payment.type_not_found", "Tipe pembayaran tidak ditemukan.");

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
        var zone = ResolveTimezone(null);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
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

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeZoneInfo ResolveTimezone(string? timezone) => TimeZoneInfo.Utc;

    private sealed record ResolvedRefundLine(
        SaleItemRow Item,
        long RefundQtyMilli,
        long UnitPriceMinor,
        long DiscountMinor,
        long TaxMinor,
        long TotalMinor);

    private sealed class SaleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long TotalMinor { get; set; }
        public string RegisterId { get; set; } = string.Empty;
        public string BusinessDayId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PriceListId { get; set; }
        public string Currency { get; set; } = "IDR";
        public int CurrencyMinorUnit { get; set; }
    }

    private sealed class SaleItemRow
    {
        public string Id { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductUnitId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public long UnitFactorMilli { get; set; }
        public long QtyMilli { get; set; }
        public long UnitPriceMinor { get; set; }
        public long DiscountMinor { get; set; }
        public long LineTotalMinor { get; set; }
        public long RefundedQtyMilli { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RateBp { get; set; }
        public string TaxRateId { get; set; } = string.Empty;
    }

    private sealed class PaymentTypeRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool MarkAsPaid { get; set; }
        public bool OpensCashDrawer { get; set; }
    }

    private sealed class RegisterRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RefundRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string OriginalSaleNumber { get; set; } = string.Empty;
        public long RefundTotalMinor { get; set; }
        public string UserName { get; set; } = string.Empty;
    }
}
