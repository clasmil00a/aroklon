using Pos.Domain.Models;
using Pos.Domain.Security;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class PermissionSetTests
{
    private const string CashierJson = """
        {"pos.sell":true,"pos.discount":true,"pos.price_override":false,"pos.void":true,
         "pos.refund":true,"pos.close_day":false,"catalog.manage":false,
         "price_lists.manage":false,"customers.manage":true,"stock.manage":false,
         "reports.view":false,"settings.manage":false,"users.manage":false,"cost.view":false}
        """;

    [Fact]
    public void All_grants_any_permission()
    {
        Assert.True(PermissionSet.All.Has(PermissionKeys.UsersManage));
        Assert.True(PermissionSet.All.Has("anything"));
    }

    [Fact]
    public void Cashier_permissions_are_parsed()
    {
        var permissions = PermissionSet.FromJson(CashierJson);
        Assert.True(permissions.Has(PermissionKeys.PosSell));
        Assert.False(permissions.Has(PermissionKeys.PosPriceOverride));
        Assert.False(permissions.Has(PermissionKeys.SettingsManage));
        Assert.False(permissions.GrantsAll);
    }

    [Fact]
    public void Unknown_or_invalid_keys_are_denied()
    {
        var permissions = PermissionSet.FromJson("""{"pos.sell":false}""");
        Assert.False(permissions.Has(PermissionKeys.PosSell));
        Assert.False(permissions.Has(""));
        Assert.False(PermissionSet.FromJson(null).Has(PermissionKeys.PosSell));
    }

    [Fact]
    public void Administrator_can_do_everything_through_session()
    {
        var session = new UserSession("u1", "admin", "Admin", "role-admin", "Administrator", true, PermissionSet.None);
        Assert.True(session.Can(PermissionKeys.UsersManage));
    }

    [Fact]
    public void Invalid_permissions_json_is_rejected()
    {
        Assert.Throws<PosException>(() => PermissionSet.FromJson("[]"));
    }
}
