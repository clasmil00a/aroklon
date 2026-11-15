using Pos.Domain.Security;

namespace Pos.Domain.Models;

public sealed record UserSession(
    string UserId,
    string Username,
    string FullName,
    string RoleId,
    string RoleName,
    bool IsAdmin,
    PermissionSet Permissions)
{
    public bool Can(string permission) => IsAdmin || Permissions.Has(permission);
}

public sealed record CompanyInfo(
    string Id,
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? Address,
    string? Phone,
    string? Email,
    string? LogoPath,
    string Currency,
    int CurrencyMinorUnit,
    string Locale,
    string Timezone,
    string? ReceiptHeader,
    string? ReceiptFooter);

public sealed record RegisterInfo(string Id, string Name, string? DefaultPriceListId);

public sealed record PriceListInfo(string Id, string Name, bool ApplyPromotions);

public sealed record PaymentTypeInfo(
    string Id,
    string Name,
    string? Icon,
    string? Shortcut,
    bool MarkAsPaid,
    bool IsDefault,
    bool OpensCashDrawer);

public sealed record UserInfo(
    string Id,
    string Username,
    string FullName,
    string RoleId,
    string RoleName,
    bool Active,
    string? LastLoginAt);

public sealed record CompanyPatch(
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? Address,
    string? Phone,
    string? Email,
    string? ReceiptHeader,
    string? ReceiptFooter);

public sealed record BackupRun(
    string Id,
    string Path,
    long SizeBytes,
    string Status,
    string? Error,
    string CreatedAt);

public sealed record AppBootstrap(
    bool NeedsOnboarding,
    UserSession? Session,
    CompanyInfo Company,
    RegisterInfo Register,
    IReadOnlyList<PriceListInfo> PriceLists,
    IReadOnlyList<PaymentTypeInfo> PaymentTypes,
    IReadOnlyDictionary<string, string> Settings);
