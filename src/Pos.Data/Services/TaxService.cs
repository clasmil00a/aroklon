using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Primitives;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Manages tax rates and computes multi-tax stacks per product line.
/// Supports tax-inclusive and tax-exclusive pricing.
/// </summary>
public sealed class TaxService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public TaxService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    /// <summary>
    /// Returns true when the pos.tax_enabled setting is true.
    /// </summary>
    public async Task<bool> IsTaxEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'pos.tax_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return string.Equals(SqlText.DisplaySetting(json), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets the pos.tax_enabled setting.
    /// </summary>
    public async Task SetTaxEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.SettingsManage);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
                VALUES ('pos.tax_enabled', 'global', '', @valueJson, @now)
                ON CONFLICT(key, scope, scope_id) DO UPDATE SET
                    value_json = excluded.value_json,
                    updated_at = excluded.updated_at;
                """,
                new { valueJson = SqlText.Serialize(enabled), now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Lists all active tax rates.
    /// </summary>
    public async Task<IReadOnlyList<TaxRateInfo>> ListTaxRatesAsync(
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaxRateRow>(new CommandDefinition(
            """
            SELECT id, name, rate_bp, active, is_default
            FROM tax_rates
            ORDER BY name;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new TaxRateInfo(
            r.Id, r.Name, r.RateBp, r.Active == 1, r.IsDefault == 1)).ToList();
    }

    /// <summary>
    /// Creates or updates a tax rate.
    /// </summary>
    public async Task UpsertTaxRateAsync(
        string id,
        string name,
        int rateBp,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException(ErrorCodes.Validation, "Nama tarif pajak wajib diisi.");
        if (rateBp < 0 || rateBp > 100000)
            throw new ValidationException(ErrorCodes.Validation, "Tarif pajak harus antara 0 dan 100000 basis poin.");

        var session = RequireSession();
        RequirePermission(session, PermissionKeys.SettingsManage);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            // If setting as default, unset other defaults.
            if (isDefault)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE tax_rates SET is_default = 0, updated_at = @now WHERE is_default = 1;",
                    new { now = SqlText.Now(now) },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO tax_rates (id, name, rate_bp, active, is_default, created_at, updated_at)
                VALUES (@id, @name, @rateBp, 1, @isDefault, @now, @now)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    rate_bp = excluded.rate_bp,
                    active = 1,
                    is_default = excluded.is_default,
                    updated_at = excluded.updated_at;
                """,
                new { id, name, rateBp, isDefault = isDefault ? 1 : 0, now = SqlText.Now(now) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Deactivates a tax rate (soft delete).
    /// </summary>
    public async Task DeactivateTaxRateAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.SettingsManage);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE tax_rates SET active = 0, updated_at = @now WHERE id = @id;",
            new { id, now = SqlText.Now(now) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the tax rates assigned to a product.
    /// </summary>
    public async Task<IReadOnlyList<TaxRateInfo>> GetProductTaxRatesAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TaxRateRow>(new CommandDefinition(
            """
            SELECT tr.id, tr.name, tr.rate_bp, tr.active, tr.is_default
            FROM product_taxes pt
            JOIN tax_rates tr ON tr.id = pt.tax_rate_id
            WHERE pt.product_id = @productId
            ORDER BY tr.rate_bp DESC;
            """,
            new { productId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new TaxRateInfo(
            r.Id, r.Name, r.RateBp, r.Active == 1, r.IsDefault == 1)).ToList();
    }

    /// <summary>
    /// Assigns tax rates to a product (replaces existing assignments).
    /// </summary>
    public async Task SetProductTaxRatesAsync(
        string productId,
        IReadOnlyList<string> taxRateIds,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var session = RequireSession();
        RequirePermission(session, PermissionKeys.SettingsManage);

        var now = _clock.UtcNow;
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM product_taxes WHERE product_id = @productId;",
                new { productId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            foreach (var taxRateId in taxRateIds)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_taxes (product_id, tax_rate_id)
                    VALUES (@productId, @taxRateId);
                    """,
                    new { productId, taxRateId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Computes the total tax for a line given all applicable tax rates.
    /// If priceIncludesTax is true, the tax is backed out from the gross amount.
    /// Returns (taxAmount, taxableBase).
    /// </summary>
    public static (long TaxAmountMinor, long TaxableBaseMinor) ComputeLineTax(
        long grossMinor,
        long discountMinor,
        IReadOnlyList<TaxRateInfo> taxRates,
        bool priceIncludesTax)
    {
        var taxableBase = grossMinor - discountMinor;
        if (taxableBase < 0) taxableBase = 0;

        if (taxRates.Count == 0)
            return (0, taxableBase);

        long totalTax;
        if (priceIncludesTax)
        {
            // Tax is included in the price. Back out the tax portion.
            // total = base + tax; tax = base * rate_sum / (10000 + rate_sum) when rate in bp
            var rateSumBp = taxRates.Sum(r => r.RateBp);
            if (rateSumBp <= 0)
                return (0, taxableBase);

            // taxableBase is the tax-inclusive amount.
            // preTax = taxableBase * 10000 / (10000 + rateSumBp)
            var preTax = MoneyMath.RoundDiv(taxableBase * 10000, 10000 + rateSumBp);
            totalTax = taxableBase - preTax;
            taxableBase = preTax;
        }
        else
        {
            var rateSumBp = taxRates.Sum(r => r.RateBp);
            totalTax = rateSumBp > 0 ? MoneyMath.PercentOf(taxableBase, rateSumBp) : 0;
        }

        return (totalTax, taxableBase);
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static void RequirePermission(UserSession session, string permission)
    {
        if (!session.Can(permission))
            throw new PermissionException("Anda tidak memiliki izin untuk mengelola tarif pajak.");
    }

    private sealed class TaxRateRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int RateBp { get; set; }
        public int Active { get; set; }
        public int IsDefault { get; set; }
    }
}

/// <summary>
/// Tax rate information for UI and calculation.
/// </summary>
public sealed record TaxRateInfo(
    string Id,
    string Name,
    int RateBp,
    bool Active,
    bool IsDefault);
