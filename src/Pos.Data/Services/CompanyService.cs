using Dapper;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

public sealed class CompanyService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CompanyService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<CompanyInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CompanyRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, name AS Name, legal_name AS LegalName, tax_number AS TaxNumber,
                           address AS Address, phone AS Phone, email AS Email, currency AS Currency,
                           currency_minor_unit AS CurrencyMinorUnit, locale AS Locale, timezone AS Timezone,
                           receipt_header AS ReceiptHeader, receipt_footer AS ReceiptFooter
                    FROM company WHERE id = 'company';
                    """,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)
            ?? throw new PosException(ErrorCodes.Database, "Identitas toko tidak ditemukan.");

        return Map(row);
    }

    public async Task<CompanyInfo> UpdateAsync(CompanyPatch patch, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException("Anda tidak memiliki izin untuk mengubah profil toko.");
        }

        var name = (patch.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 100)
        {
            throw new ValidationException("company.name_invalid", "Nama toko harus 2–100 karakter.");
        }

        var legalName = Clean(patch.LegalName, 200, "Nama badan hukum maksimal 200 karakter.");
        var taxNumber = Clean(patch.TaxNumber, 50, "NPWP maksimal 50 karakter.");
        var address = Clean(patch.Address, 500, "Alamat maksimal 500 karakter.");
        var phone = Clean(patch.Phone, 50, "Telepon maksimal 50 karakter.");
        var email = Clean(patch.Email, 200, "Email maksimal 200 karakter.");
        if (email is not null && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
        {
            throw new ValidationException("company.email_invalid", "Alamat email tidak valid.");
        }

        var receiptHeader = Clean(patch.ReceiptHeader, 500, "Header struk maksimal 500 karakter.");
        var receiptFooter = Clean(patch.ReceiptFooter, 500, "Footer struk maksimal 500 karakter.");
        var now = _clock.UtcNow;

        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE company SET
                  name = @name, legal_name = @legalName, tax_number = @taxNumber,
                  address = @address, phone = @phone, email = @email,
                  receipt_header = @receiptHeader, receipt_footer = @receiptFooter,
                  updated_at = @now
                WHERE id = 'company';
                """,
                new
                {
                    name,
                    legalName,
                    taxNumber,
                    address,
                    phone,
                    email,
                    receiptHeader,
                    receiptFooter,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new PosException(ErrorCodes.Database, "Identitas toko tidak ditemukan.");
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "settings.company_updated",
                "company",
                "company",
                new { name },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static string? Clean(string? value, int maxLength, string message)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is not null && trimmed.Length > maxLength)
        {
            throw new ValidationException(ErrorCodes.Validation, message);
        }

        return trimmed;
    }

    private static CompanyInfo Map(CompanyRow row) => new(
        row.Id,
        row.Name,
        row.LegalName,
        row.TaxNumber,
        row.Address,
        row.Phone,
        row.Email,
        row.Currency,
        row.CurrencyMinorUnit,
        row.Locale,
        row.Timezone,
        row.ReceiptHeader,
        row.ReceiptFooter);

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
}
