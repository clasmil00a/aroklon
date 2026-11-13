using System.Text.Json;

namespace Pos.Domain.Security;

public static class PermissionKeys
{
    public const string PosSell = "pos.sell";
    public const string PosDiscount = "pos.discount";
    public const string PosPriceOverride = "pos.price_override";
    public const string PosVoid = "pos.void";
    public const string PosRefund = "pos.refund";
    public const string PosCloseDay = "pos.close_day";
    public const string CatalogManage = "catalog.manage";
    public const string PriceListsManage = "price_lists.manage";
    public const string CustomersManage = "customers.manage";
    public const string StockManage = "stock.manage";
    public const string ReportsView = "reports.view";
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";
    public const string CostView = "cost.view";
}

/// <summary>Parsed view of roles.permissions_json.</summary>
public sealed class PermissionSet
{
    private readonly HashSet<string> _granted;

    private PermissionSet(bool grantsAll, IEnumerable<string> granted)
    {
        GrantsAll = grantsAll;
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    public bool GrantsAll { get; }

    public static PermissionSet All { get; } = new(true, Array.Empty<string>());

    public static PermissionSet None { get; } = new(false, Array.Empty<string>());

    public static PermissionSet FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new PosException("permissions.invalid", "Izin peran tidak valid.");
        }

        var granted = new List<string>();
        var all = false;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("all") &&
                property.Value.ValueKind == JsonValueKind.True)
            {
                all = true;
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.True)
            {
                granted.Add(property.Name);
            }
        }

        return all ? All : new PermissionSet(false, granted);
    }

    public bool Has(string permission) =>
        !string.IsNullOrWhiteSpace(permission) &&
        (GrantsAll || _granted.Contains(permission));
}
