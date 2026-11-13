using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Documents;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Pos.Domain.Primitives;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class SaleService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public SaleService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<SaleResult> ParkAsync(
        ParkSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.PosSell);
        ValidateLines(request.Lines);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
            {
                var existing = await FindByClientRequestAsync(
                    connection, transaction, request.ClientRequestId, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return existing;
                }
            }

            var company = await GetCompanyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var register = await GetRegisterAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var customer = await GetCustomerAsync(
                connection, transaction, request.CustomerId, cancellationToken).ConfigureAwait(false);
            var priceListId = customer?.PriceListId ?? register.DefaultPriceListId;
            var lines = await ResolveLinesAsync(
                connection, transaction, request.Lines, priceListId, session, cancellationToken).ConfigureAwait(false);
            var totals = CalculateTotals(lines);
            var businessDayId = await EnsureBusinessDayAsync(
                connection, transaction, register.Id, session.UserId, company.Timezone, now, cancellationToken).ConfigureAwait(false);
            var number = await NextNumberAsync(
                connection, transaction, "order", register.Id, "ORD-", now, company.Timezone, cancellationToken).ConfigureAwait(false);

            var saleId = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO sales
                  (id, number, type, status, client_request_id, register_id, business_day_id,
                   user_id, customer_id, customer_name_snapshot, customer_tax_number_snapshot,
                   price_list_id, currency, currency_minor_unit, subtotal_minor,
                   discount_kind, discount_value, discount_total_minor, tax_total_minor,
                   total_minor, paid_total_minor, change_minor, balance_minor, note,
                   created_at, updated_at)
                VALUES
                  (@id, @number, 'sale', 'open', @clientRequestId, @registerId, @businessDayId,
                   @userId, @customerId, @customerName, @customerTaxNumber,
                   @priceListId, @currency, @currencyMinorUnit, @subtotal,
                   'none', 0, @discount, @tax, @total, 0, 0, @total, @note,
                   @now, @now);
                """,
                new
                {
                    id = saleId,
                    number,
                    clientRequestId = EmptyToNull(request.ClientRequestId),
                    registerId = register.Id,
                    businessDayId,
                    userId = session.UserId,
                    customerId = customer?.Id,
                    customerName = customer?.Name,
                    customerTaxNumber = customer?.TaxNumber,
                    priceListId,
                    currency = company.Currency,
                    currencyMinorUnit = company.CurrencyMinorUnit,
                    subtotal = totals.Subtotal,
                    discount = totals.Discount,
                    tax = totals.Tax,
                    total = totals.Total,
                    note = EmptyToNull(request.Note),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await InsertSaleItemsAsync(
                connection, transaction, saleId, lines, "added", now, cancellationToken).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "sale.parked",
                "sale",
                saleId,
                new { number, total = totals.Total },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new SaleResult(
                saleId, number, "open", totals.Subtotal, totals.Discount, totals.Tax,
                totals.Total, 0, 0, false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<SaleResult> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.PosSell);
        ValidateLines(request.Lines);

        if (string.IsNullOrWhiteSpace(request.PaymentTypeId))
        {
            throw new ValidationException(ErrorCodes.Validation, "Tipe pembayaran wajib dipilih.");
        }

        if (request.TenderedMinor < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Jumlah pembayaran tidak boleh negatif.");
        }

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
            {
                var existing = await FindByClientRequestAsync(
                    connection, transaction, request.ClientRequestId, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    if (existing.Status == "open")
                    {
                        throw new ConflictException(
                            "sale.request_open",
                            "Permintaan ini masih berupa pesanan terbuka. Selesaikan pesanan tersebut terlebih dahulu.");
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return existing with { IsExisting = true };
                }
            }

            var company = await GetCompanyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var register = await GetRegisterAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var customer = await GetCustomerAsync(
                connection, transaction, request.CustomerId, cancellationToken).ConfigureAwait(false);
            var paymentType = await GetPaymentTypeAsync(
                connection, transaction, request.PaymentTypeId, cancellationToken).ConfigureAwait(false);
            var priceListId = customer?.PriceListId ?? register.DefaultPriceListId;
            var lines = await ResolveLinesAsync(
                connection, transaction, request.Lines, priceListId, session, cancellationToken).ConfigureAwait(false);
            var totals = CalculateTotals(lines);

            // Apply cart-level discount on top of line-level discounts (checkout only).
            var cartDiscountMinor = CartMath.DiscountFor(
                totals.Subtotal - totals.Discount,
                request.CartDiscountKind,
                request.CartDiscountValue);
            var totalDiscount = checked(totals.Discount + cartDiscountMinor);
            var totalWithCartDiscount = checked(totals.Subtotal - totalDiscount + totals.Tax);

            var allowNegativeStock = string.Equals(
                await GetSettingAsync(connection, transaction, "pos.negative_stock", cancellationToken).ConfigureAwait(false),
                "allow",
                StringComparison.OrdinalIgnoreCase);
            await EnsureStockAsync(connection, transaction, lines, allowNegativeStock, cancellationToken).ConfigureAwait(false);

            long paid;
            long change;
            long balance;
            if (totalWithCartDiscount == 0)
            {
                paid = 0;
                change = 0;
                balance = 0;
            }
            else if (paymentType.MarkAsPaid)
            {
                if (request.TenderedMinor < totalWithCartDiscount)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Jumlah pembayaran kurang dari total transaksi.");
                }

                paid = totalWithCartDiscount;
                change = request.TenderedMinor - totalWithCartDiscount;
                balance = 0;
            }
            else
            {
                if (customer is null)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Pembayaran kredit memerlukan pelanggan.");
                }

                paid = Math.Min(request.TenderedMinor, totalWithCartDiscount);
                change = 0;
                balance = totalWithCartDiscount - paid;
            }

            var businessDayId = await EnsureBusinessDayAsync(
                connection, transaction, register.Id, session.UserId, company.Timezone, now, cancellationToken).ConfigureAwait(false);
            var number = await NextNumberAsync(
                connection, transaction, "receipt", register.Id, "INV-", now, company.Timezone, cancellationToken).ConfigureAwait(false);

            var saleId = string.IsNullOrWhiteSpace(request.ParkedSaleId)
                ? Ids.NewId()
                : request.ParkedSaleId;

            if (string.IsNullOrWhiteSpace(request.ParkedSaleId))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO sales
                      (id, number, type, status, client_request_id, register_id, business_day_id,
                       user_id, customer_id, customer_name_snapshot, customer_tax_number_snapshot,
                       price_list_id, currency, currency_minor_unit, subtotal_minor,
                       discount_kind, discount_value, discount_total_minor, tax_total_minor,
                       total_minor, paid_total_minor, change_minor, balance_minor, note,
                       created_at, updated_at, closed_at)
                    VALUES
                      (@id, @number, 'sale', 'closed', @clientRequestId, @registerId, @businessDayId,
                       @userId, @customerId, @customerName, @customerTaxNumber,
                       @priceListId, @currency, @currencyMinorUnit, @subtotal,
                       'none', 0, @discount, @tax, @total, @paid, @change, @balance, @note,
                       @now, @now, @now);
                    """,
                    new
                    {
                        id = saleId,
                        number,
                        clientRequestId = EmptyToNull(request.ClientRequestId),
                        registerId = register.Id,
                        businessDayId,
                        userId = session.UserId,
                        customerId = customer?.Id,
                        customerName = customer?.Name,
                        customerTaxNumber = customer?.TaxNumber,
                        priceListId,
                        currency = company.Currency,
                        currencyMinorUnit = company.CurrencyMinorUnit,
                        subtotal = totals.Subtotal,
                        discount = totalDiscount,
                        tax = totals.Tax,
                        total = totalWithCartDiscount,
                        paid,
                        change,
                        balance,
                        note = EmptyToNull(request.Note),
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                var parked = await GetParkedSaleAsync(
                    connection, transaction, saleId, register.Id, cancellationToken).ConfigureAwait(false);
                if (parked is null)
                {
                    throw new NotFoundException(
                        "sale.open_not_found",
                        "Pesanan terbuka tidak ditemukan atau sudah selesai.");
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM sale_items WHERE sale_id = @saleId;",
                    new { saleId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE sales SET
                      number = @number,
                      status = 'closed',
                      client_request_id = COALESCE(client_request_id, @clientRequestId),
                      business_day_id = @businessDayId,
                      user_id = @userId,
                      customer_id = @customerId,
                      customer_name_snapshot = @customerName,
                      customer_tax_number_snapshot = @customerTaxNumber,
                      price_list_id = @priceListId,
                      subtotal_minor = @subtotal,
                      discount_total_minor = @discount,
                      tax_total_minor = @tax,
                      total_minor = @total,
                      paid_total_minor = @paid,
                      change_minor = @change,
                      balance_minor = @balance,
                      note = COALESCE(@note, note),
                      closed_at = @now,
                      updated_at = @now
                    WHERE id = @saleId;
                    """,
                    new
                    {
                        saleId,
                        number,
                        clientRequestId = EmptyToNull(request.ClientRequestId),
                        businessDayId,
                        userId = session.UserId,
                        customerId = customer?.Id,
                        customerName = customer?.Name,
                        customerTaxNumber = customer?.TaxNumber,
                        priceListId,
                        subtotal = totals.Subtotal,
                        discount = totalDiscount,
                        tax = totals.Tax,
                        total = totalWithCartDiscount,
                        paid,
                        change,
                        balance,
                        note = EmptyToNull(request.Note),
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await InsertSaleItemsAsync(
                connection, transaction, saleId, lines, "confirmed", now, cancellationToken).ConfigureAwait(false);

            foreach (var line in lines.Where(line => !line.IsService))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO stock_movements
                      (id, product_id, type, qty_delta_base_milli, unit_cost_minor,
                       reference_type, reference_id, note, user_id, created_at)
                    VALUES
                      (@id, @productId, 'sale', @qty, @cost,
                       'sale', @saleId, @note, @userId, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        productId = line.ProductId,
                        qty = -line.BaseQtyMilli,
                        cost = line.UnitCostMinor,
                        saleId,
                        note = line.Name,
                        userId = session.UserId,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var appliedPayment = paymentType.MarkAsPaid
                ? Math.Min(request.TenderedMinor, totals.Total)
                : paid;
            if (appliedPayment > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO payments
                      (id, sale_id, payment_type_id, kind, amount_minor, tendered_minor,
                       change_minor, status, created_by, created_at, updated_at)
                    VALUES
                      (@id, @saleId, @paymentTypeId, 'tender', @amount, @tendered,
                       @change, 'captured', @userId, @now, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        saleId,
                        paymentTypeId = paymentType.Id,
                        amount = appliedPayment,
                        tendered = request.TenderedMinor,
                        change,
                        userId = session.UserId,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE business_days SET
                  sales_count = sales_count + 1,
                  taxable_total_minor = taxable_total_minor + @taxable,
                  discount_total_minor = discount_total_minor + @discount,
                  grand_total_minor = grand_total_minor + @total,
                  updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    taxable = totals.Subtotal - totalDiscount,
                    discount = totalDiscount,
                    total = totalWithCartDiscount,
                    now = SqlText.Now(now),
                    id = businessDayId
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "sale.completed",
                "sale",
                saleId,
                new { number, total = totals.Total, paid, change, service = request.ParkedSaleId is not null },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new SaleResult(
                saleId, number, "closed", totals.Subtotal, totalDiscount, totals.Tax,
                totalWithCartDiscount, paid, change, false);
        }
        catch (SqliteException exception) when (
            exception.SqliteErrorCode == 19 &&
            exception.Message.Contains("client_request_id", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindByClientRequestIdAsync(
                request.ClientRequestId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return existing with { IsExisting = true };
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<OpenSaleSummary>> ListOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.PosSell);
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<OpenSaleRow>(new CommandDefinition(
            """
            SELECT
              s.id AS Id,
              s.number AS Number,
              s.created_at AS CreatedAt,
              s.customer_name_snapshot AS CustomerName,
              s.user_name AS UserName,
              COUNT(si.id) AS ItemCount,
              s.total_minor AS TotalMinor,
              s.note AS Note
            FROM v_open_sales s
            LEFT JOIN sale_items si ON si.sale_id = s.id
            GROUP BY s.id
            ORDER BY s.created_at DESC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(row => new OpenSaleSummary(
            row.Id, row.Number, row.CreatedAt, row.CustomerName, row.UserName,
            row.ItemCount, row.TotalMinor, row.Note)).ToList();
    }

    public async Task VoidOpenAsync(
        string saleId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.PosVoid);
        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var requireReason = string.Equals(
                await GetSettingAsync(connection, transaction, "pos.require_void_reason", cancellationToken).ConfigureAwait(false),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (requireReason && string.IsNullOrWhiteSpace(reason))
            {
                throw new ValidationException(ErrorCodes.Validation, "Alasan pembatalan wajib diisi.");
            }

            var sale = await GetParkedSaleAsync(connection, transaction, saleId, null, cancellationToken).ConfigureAwait(false);
            if (sale is null)
            {
                throw new NotFoundException("sale.open_not_found", "Pesanan terbuka tidak ditemukan.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sales
                SET status = 'voided', void_reason = @reason, updated_at = @now
                WHERE id = @saleId;
                """,
                new { saleId, reason = EmptyToNull(reason), now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "sale.parked_voided",
                "sale",
                saleId,
                new { number = sale.Number, reason },
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

    public async Task<IReadOnlyList<SaleSummary>> ListAsync(
        SaleFilter filter,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PosSell) && !session.Can(PermissionKeys.ReportsView))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk melihat riwayat penjualan.");
        }

        var from = filter.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = filter.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var query = (filter.Query ?? string.Empty).Trim();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<SaleRow>(new CommandDefinition(
            """
            SELECT
              s.id AS Id,
              s.number AS Number,
              s.status AS Status,
              s.type AS Type,
              s.created_at AS CreatedAt,
              s.customer_name_snapshot AS CustomerName,
              u.full_name AS UserName,
              COUNT(si.id) AS ItemCount,
              s.total_minor AS TotalMinor,
              s.paid_total_minor AS PaidMinor
            FROM sales s
            JOIN users u ON u.id = s.user_id
            LEFT JOIN business_days bd ON bd.id = s.business_day_id
            LEFT JOIN sale_items si ON si.sale_id = s.id AND si.status <> 'voided'
            WHERE s.status IN ('closed','voided','refunded','partially_refunded')
              AND (@from IS NULL OR COALESCE(bd.business_date, date(s.created_at)) >= @from)
              AND (@to IS NULL OR COALESCE(bd.business_date, date(s.created_at)) <= @to)
              AND (
                @query = ''
                OR s.number LIKE @like
                OR s.customer_name_snapshot LIKE @like
                OR u.full_name LIKE @like
              )
            GROUP BY s.id
            ORDER BY s.created_at DESC
            LIMIT @limit;
            """,
            new
            {
                from,
                to,
                query,
                like = "%" + query + "%",
                limit = Math.Clamp(filter.Limit, 1, 1000)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(row => new SaleSummary(
            row.Id, row.Number, row.Status, row.Type, row.CreatedAt,
            row.CustomerName, row.UserName, row.ItemCount, row.TotalMinor, row.PaidMinor)).ToList();
    }

    public async Task<SaleDetail> GetAsync(
        string saleId,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.PosSell) && !session.Can(PermissionKeys.ReportsView))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk melihat transaksi.");
        }

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sale = await connection.QuerySingleOrDefaultAsync<SaleDetailRow>(new CommandDefinition(
            """
            SELECT
              s.id AS Id,
              s.number AS Number,
              s.status AS Status,
              s.type AS Type,
              s.created_at AS CreatedAt,
              s.customer_name_snapshot AS CustomerName,
              u.full_name AS UserName,
              r.name AS RegisterName,
              s.subtotal_minor AS SubtotalMinor,
              s.discount_total_minor AS DiscountMinor,
              s.tax_total_minor AS TaxMinor,
              s.total_minor AS TotalMinor,
              s.paid_total_minor AS PaidMinor,
              s.change_minor AS ChangeMinor,
              s.balance_minor AS BalanceMinor,
              s.note AS Note
            FROM sales s
            JOIN users u ON u.id = s.user_id
            JOIN registers r ON r.id = s.register_id
            WHERE s.id = @saleId;
            """,
            new { saleId },
            cancellationToken: cancellationToken)).ConfigureAwait(false)
            ?? throw new NotFoundException("sale.not_found", "Transaksi tidak ditemukan.");

        var lines = (await connection.QueryAsync<SaleLineRow>(new CommandDefinition(
            """
            SELECT
              id AS Id,
              product_id AS ProductId,
              product_unit_id AS ProductUnitId,
              name_snapshot AS Name,
              code_snapshot AS Code,
              unit_name_snapshot AS UnitName,
              unit_factor_milli AS UnitFactorMilli,
              qty_milli AS QtyMilli,
              base_qty_milli AS BaseQtyMilli,
              unit_price_minor AS UnitPriceMinor,
              discount_total_minor AS DiscountMinor,
              line_total_minor AS TotalMinor,
              status AS Status,
              comment AS Comment
            FROM sale_items
            WHERE sale_id = @saleId
            ORDER BY sort_order, created_at;
            """,
            new { saleId },
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var payments = (await connection.QueryAsync<SalePaymentRow>(new CommandDefinition(
            """
            SELECT
              p.id AS Id,
              pt.name AS PaymentTypeName,
              p.amount_minor AS AmountMinor,
              p.tendered_minor AS TenderedMinor,
              p.change_minor AS ChangeMinor,
              p.created_at AS CreatedAt
            FROM payments p
            JOIN payment_types pt ON pt.id = p.payment_type_id
            WHERE p.sale_id = @saleId AND p.status = 'captured'
            ORDER BY p.created_at;
            """,
            new { saleId },
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return new SaleDetail(
            sale.Id,
            sale.Number,
            sale.Status,
            sale.Type,
            sale.CreatedAt,
            sale.CustomerName,
            sale.UserName,
            sale.RegisterName,
            sale.SubtotalMinor,
            sale.DiscountMinor,
            sale.TaxMinor,
            sale.TotalMinor,
            sale.PaidMinor,
            sale.ChangeMinor,
            sale.BalanceMinor,
            sale.Note,
            lines.Select(line => new SaleLineDetail(
                line.Id, line.ProductId, line.ProductUnitId, line.Name, line.Code,
                line.UnitName, line.UnitFactorMilli, line.QtyMilli, line.BaseQtyMilli,
                line.UnitPriceMinor, line.DiscountMinor, line.TotalMinor,
                line.Status, line.Comment)).ToList(),
            payments.Select(payment => new SalePaymentDetail(
                payment.Id, payment.PaymentTypeName, payment.AmountMinor,
                payment.TenderedMinor, payment.ChangeMinor, payment.CreatedAt)).ToList());
    }

    public async Task<ReceiptDocument> GetReceiptAsync(
        string saleId,
        CancellationToken cancellationToken = default)
    {
        var sale = await GetAsync(saleId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var company = await GetCompanyAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var showUnits = string.Equals(
            await GetSettingAsync(connection, null, "receipt.show_units", cancellationToken).ConfigureAwait(false),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var header = await GetSettingAsync(connection, null, "receipt.header", cancellationToken).ConfigureAwait(false);
        var footer = await GetSettingAsync(connection, null, "receipt.footer", cancellationToken).ConfigureAwait(false);

        var timezone = ResolveTimezone(company.Timezone);
        var createdAt = SqlText.ParseTime(sale.CreatedAt) ?? _clock.UtcNow;
        var local = TimeZoneInfo.ConvertTime(createdAt, timezone);

        var lines = sale.Lines
            .Where(line => line.Status != "voided")
            .Select(line => new ReceiptLine(
                line.Name,
                line.QtyMilli,
                line.UnitName,
                line.UnitPriceMinor,
                line.DiscountMinor,
                line.TotalMinor))
            .ToList();
        var payments = sale.Payments
            .Select(payment => new ReceiptPayment(
                payment.PaymentTypeName, payment.AmountMinor,
                payment.TenderedMinor, payment.ChangeMinor))
            .ToList();

        return new ReceiptDocument(
            company.Name,
            company.Address,
            company.Phone,
            company.TaxNumber,
            string.IsNullOrWhiteSpace(header) ? company.ReceiptHeader : header,
            string.IsNullOrWhiteSpace(footer) ? company.ReceiptFooter : footer,
            sale.Number,
            ReceiptRenderer.FormatDate(local, company.Locale),
            sale.UserName,
            sale.RegisterName,
            sale.CustomerName,
            showUnits,
            lines,
            sale.SubtotalMinor,
            sale.DiscountMinor,
            sale.TaxMinor,
            sale.TotalMinor,
            sale.PaidMinor,
            sale.ChangeMinor,
            sale.BalanceMinor,
            payments,
            company.Currency,
            company.CurrencyMinorUnit);
    }

    public static string RenderReceipt(ReceiptDocument receipt, int width = 42) =>
        ReceiptRenderer.Render(receipt, width);

    private async Task<IReadOnlyList<ResolvedLine>> ResolveLinesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<SaleLineInput> inputs,
        string? priceListId,
        UserSession session,
        CancellationToken cancellationToken)
    {
        var allowPriceOverrideSetting = string.Equals(
            await GetSettingAsync(connection, transaction, "pos.allow_price_override", cancellationToken).ConfigureAwait(false),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var discountEnabled = !string.Equals(
            await GetSettingAsync(connection, transaction, "pos.discount_enabled", cancellationToken).ConfigureAwait(false),
            "false",
            StringComparison.OrdinalIgnoreCase);

        var resolved = new List<ResolvedLine>(inputs.Count);
        foreach (var input in inputs)
        {
            if (input.QtyMilli <= 0)
            {
                throw new ValidationException(ErrorCodes.Validation, "Jumlah setiap baris harus lebih dari nol.");
            }

            var row = await connection.QuerySingleOrDefaultAsync<ResolvedRow>(new CommandDefinition(
                """
                SELECT
                  p.id AS ProductId,
                  p.name AS Name,
                  p.code AS Code,
                  p.is_service AS IsService,
                  p.price_change_allowed AS PriceChangeAllowed,
                  p.cost_minor AS BaseCostMinor,
                  p.price_minor AS BasePriceMinor,
                  pu.id AS ProductUnitId,
                  pu.factor_to_base_milli AS UnitFactorMilli,
                  pu.price_minor AS UnitPriceMinor,
                  u.name AS UnitName,
                  bu.name AS BaseUnitName,
                  pl.price_minor AS ListPriceMinor
                FROM product_units pu
                JOIN products p ON p.id = pu.product_id
                JOIN units u ON u.id = pu.unit_id
                JOIN product_units bpu ON bpu.product_id = p.id AND bpu.is_base = 1
                JOIN units bu ON bu.id = bpu.unit_id
                LEFT JOIN price_list_items pl
                  ON pl.product_unit_id = pu.id AND pl.price_list_id = @priceListId
                WHERE pu.id = @productUnitId
                  AND pu.product_id = @productId
                  AND pu.active = 1
                  AND p.active = 1;
                """,
                new
                {
                    productUnitId = input.ProductUnitId,
                    productId = input.ProductId,
                    priceListId
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)
                ?? throw new NotFoundException(
                    "product.not_found",
                    "Produk atau satuan jual tidak ditemukan.");

            var price = Pos.Domain.Pricing.PriceResolver.Resolve(
                row.ListPriceMinor,
                row.UnitPriceMinor,
                row.BasePriceMinor,
                row.UnitFactorMilli).PriceMinor;

            var overridden = false;
            if (input.UnitPriceOverrideMinor is not null && input.UnitPriceOverrideMinor.Value != price)
            {
                if (!row.PriceChangeAllowed ||
                    (!session.Can(PermissionKeys.PosPriceOverride) && !allowPriceOverrideSetting))
                {
                    throw new PermissionException("Harga produk ini tidak boleh diubah.");
                }

                price = input.UnitPriceOverrideMinor.Value;
                overridden = true;
            }

            var gross = MoneyMath.LineTotal(input.QtyMilli, price);
            if (input.DiscountKind != DiscountKind.None)
            {
                if (!discountEnabled)
                {
                    throw new ValidationException(
                        ErrorCodes.Validation,
                        "Diskon dinonaktifkan untuk toko ini.");
                }

                if (!session.Can(PermissionKeys.PosDiscount))
                {
                    throw new PermissionException("Anda tidak memiliki izin memberi diskon.");
                }
            }

            var discount = CartMath.DiscountFor(gross, input.DiscountKind, input.DiscountValue);
            var baseQty = UnitMath.ToBase(input.QtyMilli, row.UnitFactorMilli);
            var unitCost = MoneyMath.RoundDiv(row.BaseCostMinor * row.UnitFactorMilli, 1000);

            // Resolve tax rate: input override, then product's default tax rate.
            var taxRateId = (await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                """
                SELECT tr.id
                FROM product_taxes pt
                JOIN tax_rates tr ON tr.id = pt.tax_rate_id AND tr.active = 1
                WHERE pt.product_id = @productId
                ORDER BY tr.rate_bp DESC
                LIMIT 1;
                """,
                new { productId = row.ProductId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false));
            var rateBp = taxRateId is not null
                ? (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT rate_bp FROM tax_rates WHERE id = @id AND active = 1;",
                    new { id = taxRateId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
                : 0;
            var taxableBase = gross - discount;
            var tax = rateBp > 0 ? MoneyMath.PercentOf(taxableBase, rateBp) : 0;

            resolved.Add(new ResolvedLine(
                row.ProductId,
                row.ProductUnitId,
                row.Name,
                row.Code,
                row.UnitName,
                row.BaseUnitName,
                row.UnitFactorMilli,
                input.QtyMilli,
                baseQty,
                price,
                unitCost,
                input.DiscountKind,
                input.DiscountValue,
                discount,
                tax,
                gross - discount + tax,
                taxRateId,
                rateBp,
                input.Comment,
                row.IsService,
                overridden));
        }

        return resolved;
    }

    private static Totals CalculateTotals(IReadOnlyList<ResolvedLine> lines)
    {
        var subtotal = lines.Sum(line => line.GrossMinor);
        var discount = lines.Sum(line => line.DiscountMinor);
        var tax = lines.Sum(line => line.TaxMinor);
        return new Totals(subtotal, discount, tax, checked(subtotal - discount + tax));
    }

    private static async Task EnsureStockAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<ResolvedLine> lines,
        bool allowNegative,
        CancellationToken cancellationToken)
    {
        if (allowNegative)
        {
            return;
        }

        var required = lines
            .Where(line => !line.IsService)
            .GroupBy(line => line.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Name = group.First().Name,
                Unit = group.First().BaseUnitName,
                Qty = group.Sum(line => line.BaseQtyMilli)
            });

        foreach (var item in required)
        {
            var stock = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COALESCE(SUM(qty_delta_base_milli), 0) FROM stock_movements WHERE product_id = @productId;",
                new { productId = item.ProductId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (stock < item.Qty)
            {
                throw new ConflictException(
                    "sale.insufficient_stock",
                    "Stok " + item.Name + " tidak cukup. Tersedia " +
                    UnitMath.FormatQuantity(stock, item.Unit) +
                    ", dibutuhkan " + UnitMath.FormatQuantity(item.Qty, item.Unit) + ".");
            }
        }
    }

    private static async Task InsertSaleItemsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string saleId,
        IReadOnlyList<ResolvedLine> lines,
        string status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sort = 0;
        foreach (var line in lines)
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
                  (@id, @saleId, @productId, @productUnitId, @name, @code,
                   @unitName, @factor, @qty, @baseQty,
                   @unitPrice, @unitCost, @discountKind, @discountValue,
                   @discountTotal, @taxTotal, @lineTotal, @status,
                   @comment, @sort, @now, @now);
                """,
                new
                {
                    id = saleItemId,
                    saleId,
                    productId = line.ProductId,
                    productUnitId = line.ProductUnitId,
                    name = line.Name,
                    code = line.Code,
                    unitName = line.UnitName,
                    factor = line.UnitFactorMilli,
                    qty = line.QtyMilli,
                    baseQty = line.BaseQtyMilli,
                    unitPrice = line.UnitPriceMinor,
                    unitCost = line.UnitCostMinor,
                    discountKind = Discount.ToDatabase(line.DiscountKind),
                    discountValue = line.DiscountValue,
                    discountTotal = line.DiscountMinor,
                    taxTotal = line.TaxMinor,
                    lineTotal = line.TotalMinor,
                    status,
                    comment = EmptyToNull(line.Comment),
                    sort = sort++,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Insert per-line tax if applicable.
            if (line.TaxMinor > 0 && line.TaxRateId is not null)
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
                        taxRateId = line.TaxRateId,
                        name = line.Name,
                        rateBp = line.RateBp,
                        taxableBase = line.GrossMinor - line.DiscountMinor,
                        taxAmount = line.TaxMinor,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> EnsureBusinessDayAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string registerId,
        string userId,
        string timezone,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var zone = ResolveTimezone(timezone);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var dateKey = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var open = await connection.QuerySingleOrDefaultAsync<BusinessDayRow>(new CommandDefinition(
            """
            SELECT id AS Id, business_date AS BusinessDate
            FROM business_days
            WHERE register_id = @registerId AND status = 'open'
            ORDER BY opened_at DESC
            LIMIT 1;
            """,
            new { registerId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (open is not null)
        {
            if (open.BusinessDate == dateKey)
            {
                return open.Id;
            }

            var expectedCash = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                SELECT
                  bd.opening_cash_minor
                  + COALESCE((
                      SELECT SUM(p.amount_minor)
                      FROM payments p
                      JOIN sales s ON s.id = p.sale_id
                      JOIN payment_types pt ON pt.id = p.payment_type_id
                      WHERE s.business_day_id = bd.id
                        AND p.status = 'captured'
                        AND pt.opens_cash_drawer = 1
                    ), 0)
                  + COALESCE((
                      SELECT SUM(CASE WHEN direction = 'in' THEN amount_minor ELSE -amount_minor END)
                      FROM cash_movements cm WHERE cm.business_day_id = bd.id
                    ), 0)
                FROM business_days bd WHERE bd.id = @id;
                """,
                new { id = open.Id },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE business_days
                SET status = 'closed', closed_at = @now, closed_by = @userId,
                    expected_cash_minor = @expected, updated_at = @now
                WHERE id = @id;
                """,
                new { id = open.Id, now = SqlText.Now(now), userId, expected = Math.Max(0, expectedCash) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

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

    private async Task<string> NextNumberAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string type,
        string registerId,
        string prefix,
        DateTimeOffset now,
        string timezone,
        CancellationToken cancellationToken)
    {
        var localDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, ResolveTimezone(timezone)).DateTime);
        var periodKey = localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var sequence = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO document_sequences
              (id, document_type, register_id, period_key, prefix, next_number,
               reset_on_day_close, created_at, updated_at)
            VALUES
              (@id, @type, @registerId, @periodKey, @prefix, 2, 0, @now, @now)
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

    private async Task<CompanyRow> GetCompanyAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleAsync<CompanyRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, legal_name AS LegalName, tax_number AS TaxNumber,
                   address AS Address, phone AS Phone, email AS Email, currency AS Currency,
                   currency_minor_unit AS CurrencyMinorUnit, locale AS Locale,
                   timezone AS Timezone, receipt_header AS ReceiptHeader,
                   receipt_footer AS ReceiptFooter
            FROM company WHERE id = 'company';
            """,
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<RegisterRow> GetRegisterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<RegisterRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, default_price_list_id AS DefaultPriceListId
            FROM registers WHERE active = 1 ORDER BY created_at LIMIT 1;
            """,
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false)
        ?? throw new PosException(ErrorCodes.Database, "Register aktif tidak ditemukan.");

    private async Task<CustomerRow?> GetCustomerAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? customerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        return await connection.QuerySingleOrDefaultAsync<CustomerRow>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, tax_number AS TaxNumber,
                   price_list_id AS PriceListId
            FROM customers WHERE id = @customerId AND active = 1;
            """,
            new { customerId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false)
            ?? throw new NotFoundException("customer.not_found", "Pelanggan tidak ditemukan.");
    }

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

    private async Task<ParkedSaleRow?> GetParkedSaleAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string saleId,
        string? registerId,
        CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<ParkedSaleRow>(new CommandDefinition(
            """
            SELECT id AS Id, number AS Number
            FROM sales
            WHERE id = @saleId
              AND status = 'open'
              AND type = 'sale'
              AND (@registerId IS NULL OR register_id = @registerId);
            """,
            new { saleId, registerId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

    private async Task<SaleResult?> FindByClientRequestAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<SaleResultRow>(new CommandDefinition(
            """
            SELECT id AS Id, number AS Number, status AS Status, subtotal_minor AS Subtotal,
                   discount_total_minor AS Discount, tax_total_minor AS Tax,
                   total_minor AS Total, paid_total_minor AS Paid, change_minor AS Change
            FROM sales WHERE client_request_id = @clientRequestId;
            """,
            new { clientRequestId },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null
            ? null
            : new SaleResult(
                row.Id, row.Number, row.Status, row.Subtotal, row.Discount,
                row.Tax, row.Total, row.Paid, row.Change, true);
    }

    private async Task<SaleResult?> FindByClientRequestIdAsync(
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await FindByClientRequestAsync(connection, null, clientRequestId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetSettingAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = @key AND scope = 'global' AND scope_id = '';",
            new { key },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return SqlText.DisplaySetting(json);
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static void RequirePermission(UserSession session, string permission)
    {
        if (!session.Can(permission))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk melakukan transaksi ini.");
        }
    }

    private static void ValidateLines(IReadOnlyList<SaleLineInput> lines)
    {
        if (lines.Count == 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Keranjang masih kosong.");
        }

        if (lines.Count > 500)
        {
            throw new ValidationException(ErrorCodes.Validation, "Terlalu banyak baris dalam satu transaksi.");
        }

        if (lines.Any(line => line.QtyMilli <= 0))
        {
            throw new ValidationException(ErrorCodes.Validation, "Jumlah setiap baris harus lebih dari nol.");
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeZoneInfo ResolveTimezone(string? timezone)
    {
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private sealed record Totals(long Subtotal, long Discount, long Tax, long Total);

    private sealed record ResolvedLine(
        string ProductId,
        string ProductUnitId,
        string Name,
        string? Code,
        string UnitName,
        string BaseUnitName,
        long UnitFactorMilli,
        long QtyMilli,
        long BaseQtyMilli,
        long UnitPriceMinor,
        long UnitCostMinor,
        DiscountKind DiscountKind,
        long DiscountValue,
        long DiscountMinor,
        long TaxMinor,
        long TotalMinor,
        string? TaxRateId,
        int RateBp,
        string? Comment,
        bool IsService,
        bool PriceOverridden)
    {
        public long GrossMinor => MoneyMath.LineTotal(QtyMilli, UnitPriceMinor);
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

    private sealed class CustomerRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
        public string? PriceListId { get; set; }
    }

    private sealed class PaymentTypeRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool MarkAsPaid { get; set; }
        public bool OpensCashDrawer { get; set; }
    }

    private sealed class BusinessDayRow
    {
        public string Id { get; set; } = string.Empty;
        public string BusinessDate { get; set; } = string.Empty;
    }

    private sealed class ParkedSaleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
    }

    private sealed class SaleResultRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long Subtotal { get; set; }
        public long Discount { get; set; }
        public long Tax { get; set; }
        public long Total { get; set; }
        public long Paid { get; set; }
        public long Change { get; set; }
    }

    private sealed class ResolvedRow
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public bool IsService { get; set; }
        public bool PriceChangeAllowed { get; set; }
        public long BaseCostMinor { get; set; }
        public long BasePriceMinor { get; set; }
        public string ProductUnitId { get; set; } = string.Empty;
        public long UnitFactorMilli { get; set; }
        public long? UnitPriceMinor { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string BaseUnitName { get; set; } = string.Empty;
        public long? ListPriceMinor { get; set; }
    }

    private sealed class SaleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public long TotalMinor { get; set; }
        public long PaidMinor { get; set; }
    }

    private sealed class OpenSaleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public long TotalMinor { get; set; }
        public string? Note { get; set; }
    }

    private sealed class SaleDetailRow
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string RegisterName { get; set; } = string.Empty;
        public long SubtotalMinor { get; set; }
        public long DiscountMinor { get; set; }
        public long TaxMinor { get; set; }
        public long TotalMinor { get; set; }
        public long PaidMinor { get; set; }
        public long ChangeMinor { get; set; }
        public long BalanceMinor { get; set; }
        public string? Note { get; set; }
    }

    private sealed class SaleLineRow
    {
        public string Id { get; set; } = string.Empty;
        public string? ProductId { get; set; }
        public string? ProductUnitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public long UnitFactorMilli { get; set; }
        public long QtyMilli { get; set; }
        public long BaseQtyMilli { get; set; }
        public long UnitPriceMinor { get; set; }
        public long DiscountMinor { get; set; }
        public long TotalMinor { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }

    private sealed class SalePaymentRow
    {
        public string Id { get; set; } = string.Empty;
        public string PaymentTypeName { get; set; } = string.Empty;
        public long AmountMinor { get; set; }
        public long TenderedMinor { get; set; }
        public long ChangeMinor { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
