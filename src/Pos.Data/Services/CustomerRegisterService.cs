using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class CustomerService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CustomerService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CustomerSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CustomerRow>(new CommandDefinition(
            """
            SELECT
              c.id AS Id,
              c.code AS Code,
              c.name AS Name,
              c.type AS Type,
              c.price_list_id AS PriceListId,
              pl.name AS PriceListName,
              c.discount_kind AS DiscountKind,
              c.discount_value AS DiscountValue,
              c.active AS Active
            FROM customers c
            LEFT JOIN price_lists pl ON pl.id = c.price_list_id
            ORDER BY c.name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new CustomerSummary(
            r.Id, r.Code, r.Name, r.Type, r.PriceListId, r.PriceListName,
            Disc.ToDatabase(r.DiscountKind), r.DiscountValue, r.Active)).ToList();
    }

    public async Task<IReadOnlyList<CustomerSummary>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CustomerRow>(new CommandDefinition(
            """
            SELECT
              c.id AS Id,
              c.code AS Code,
              c.name AS Name,
              c.type AS Type,
              c.price_list_id AS PriceListId,
              pl.name AS PriceListName,
              c.discount_kind AS DiscountKind,
              c.discount_value AS DiscountValue,
              c.active AS Active
            FROM customers c
            LEFT JOIN price_lists pl ON pl.id = c.price_list_id
            WHERE c.active = 1
            ORDER BY c.name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new CustomerSummary(
            r.Id, r.Code, r.Name, r.Type, r.PriceListId, r.PriceListName,
            Disc.ToDatabase(r.DiscountKind), r.DiscountValue, r.Active)).ToList();
    }

    public async Task UpdateAsync(
        string customerId,
        CustomerUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.CustomersManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola pelanggan.");
        }

        Validate(request);
        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE customers
                SET price_list_id = @priceListId,
                    discount_kind = @discountKind,
                    discount_value = @discountValue,
                    active = @active,
                    updated_at = @now
                WHERE id = @id;
                """,
                new
                {
                    id = customerId,
                    priceListId = string.IsNullOrWhiteSpace(request.PriceListId) ? null : request.PriceListId,
                    discountKind = Disc.ToDatabase(request.DiscountKind),
                    discountValue = request.DiscountValue ?? 0,
                    active = request.Active ?? true ? 1 : 0,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("customer.not_found", "Pelanggan tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "customer.updated",
                "customer",
                customerId,
                new { priceListId = request.PriceListId, discountKind = request.DiscountKind, discountValue = request.DiscountValue, active = request.Active },
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

    private static void Validate(CustomerUpdateRequest request)
    {
        if (request.DiscountValue is not null && request.DiscountValue.Value < 0)
        {
            throw new ValidationException(ErrorCodes.Validation, "Diskon tidak boleh negatif.");
        }
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static class Disc
    {
        public static string ToDatabase(string? kind) => kind switch
        {
            "percent" => "percent",
            "amount" => "amount",
            _ => "none"
        };
    }

    private sealed class CustomerRow
    {
        public string Id { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? PriceListId { get; set; }
        public string? PriceListName { get; set; }
        public string DiscountKind { get; set; } = "none";
        public long DiscountValue { get; set; }
        public bool Active { get; set; }
    }
}

public sealed class RegisterService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public RegisterService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RegisterSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RegisterRow>(new CommandDefinition(
            """
            SELECT
              r.id AS Id,
              r.name AS Name,
              r.default_price_list_id AS DefaultPriceListId,
              pl.name AS DefaultPriceListName,
              r.active AS Active
            FROM registers r
            LEFT JOIN price_lists pl ON pl.id = r.default_price_list_id
            ORDER BY r.name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new RegisterSummary(r.Id, r.Name, r.DefaultPriceListId, r.DefaultPriceListName, r.Active)).ToList();
    }

    public async Task UpdateDefaultPriceListAsync(
        string registerId,
        RegisterUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengatur register.");
        }

        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (request.DefaultPriceListId is not null)
            {
                var exists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT COUNT(*) FROM price_lists WHERE id = @id AND active = 1;",
                    new { id = request.DefaultPriceListId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (exists == 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw new NotFoundException("price_list.not_found", "Daftar harga tidak ditemukan atau tidak aktif.");
                }
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE registers
                SET default_price_list_id = @defaultPriceListId, updated_at = @now
                WHERE id = @id;
                """,
                new { id = registerId, defaultPriceListId = request.DefaultPriceListId, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new NotFoundException("register.not_found", "Register tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "register.updated",
                "register",
                registerId,
                new { defaultPriceListId = request.DefaultPriceListId },
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

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private sealed class RegisterRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DefaultPriceListId { get; set; }
        public string? DefaultPriceListName { get; set; }
        public bool Active { get; set; }
    }
}
