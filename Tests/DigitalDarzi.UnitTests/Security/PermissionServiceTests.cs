using Application.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Xunit;

namespace DigitalDarzi.UnitTests.Security;

/// <summary>
/// Tests the pure permission-resolution rule (Effective) directly — no database, no mocks needed
/// since the method takes its overrides as a plain list.
/// </summary>
public class PermissionServiceTests
{
    private static TenantRolePermission Override(string role, string permission, bool granted) => new()
    {
        RoleName = role, Permission = permission, Granted = granted
    };

    // ───────────────────────────────────────────────────────────────
    // No overrides: falls back to the role default
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Effective_NoOverrides_DefaultGrantedPermission_ReturnsTrue()
        => PermissionService.Effective(AppRoles.Manager, Permissions.OrdersCreate, overrides: Array.Empty<TenantRolePermission>())
            .Should().BeTrue("OrdersCreate is a default Manager permission");

    [Fact]
    public void Effective_NoOverrides_PermissionNotInDefaults_ReturnsFalse()
        => PermissionService.Effective(AppRoles.Karigar, Permissions.PaymentsRecord, overrides: Array.Empty<TenantRolePermission>())
            .Should().BeFalse("Karigar's default set does not include recording payments");

    // ───────────────────────────────────────────────────────────────
    // Owner: never customisable, regardless of what a rogue override row might say
    // ───────────────────────────────────────────────────────────────

    // Rule: the Owner role is not in CustomisableRoles, so no override can ever change what it can do.
    // Why:  this is a defence-in-depth check — even if a bad migration or manual database edit
    //       created an override row for "Owner", it must never be honoured. Owner permissions
    //       come only from the hard-coded default (every permission).
    [Fact]
    public void Effective_OwnerRole_IgnoresOverrideRows_EvenARevocation()
    {
        var overrides = new[] { Override(AppRoles.Owner, Permissions.UsersManage, granted: false) };

        PermissionService.Effective(AppRoles.Owner, Permissions.UsersManage, overrides).Should().BeTrue();
    }

    // ───────────────────────────────────────────────────────────────
    // Non-delegable permissions: Owner-only no matter what an override row claims
    // ───────────────────────────────────────────────────────────────

    // Rule: a permission in Permissions.NonDelegable can never be granted to a customisable role,
    //       even by an explicit override row that says Granted = true.
    // Why:  this is the guard that stops an Owner from accidentally (or a bad actor from
    //       maliciously) handing UsersManage or BillingView to a Manager through the permissions
    //       UI — those stay Owner-only by construction, not by convention.
    [Fact]
    public void Effective_NonDelegablePermission_OverrideGrantIsIgnored()
    {
        Permissions.NonDelegable.Should().Contain(Permissions.UsersManage, "this test assumes UsersManage is non-delegable");
        var overrides = new[] { Override(AppRoles.Manager, Permissions.UsersManage, granted: true) };

        PermissionService.Effective(AppRoles.Manager, Permissions.UsersManage, overrides).Should().BeFalse();
    }

    // ───────────────────────────────────────────────────────────────
    // Customisable roles: an override wins over the default in both directions
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Effective_CustomisableRole_OverrideRevokesADefaultPermission()
    {
        Permissions.Defaults[AppRoles.Manager].Should().Contain(Permissions.OrdersCancel, "this test assumes OrdersCancel is a Manager default");
        var overrides = new[] { Override(AppRoles.Manager, Permissions.OrdersCancel, granted: false) };

        PermissionService.Effective(AppRoles.Manager, Permissions.OrdersCancel, overrides).Should().BeFalse();
    }

    [Fact]
    public void Effective_CustomisableRole_OverrideGrantsANonDefaultDelegablePermission()
    {
        Permissions.Defaults[AppRoles.Receptionist].Should().NotContain(Permissions.OrdersStatus, "this test assumes Receptionist lacks OrdersStatus by default");
        var overrides = new[] { Override(AppRoles.Receptionist, Permissions.OrdersStatus, granted: true) };

        PermissionService.Effective(AppRoles.Receptionist, Permissions.OrdersStatus, overrides).Should().BeTrue();
    }

    // Rule: an override row is scoped to the exact role it names — it must not leak to any other role.
    // Why:  the permissions screen edits one role's row at a time; if the RoleName filter in
    //       Effective were ever dropped, granting Manager a permission would silently hand the
    //       same permission to Receptionist and Karigar too, without the Owner ever choosing that.
    [Fact]
    public void Effective_GrantOverrideForOneRole_DoesNotLeakToAnotherRole()
    {
        // FinanceDelete belongs to neither role by default, so a leaked grant is unambiguous:
        // without the RoleName filter, Receptionist would incorrectly read as granted too.
        Permissions.Defaults[AppRoles.Manager].Should().NotContain(Permissions.FinanceDelete);
        Permissions.Defaults[AppRoles.Receptionist].Should().NotContain(Permissions.FinanceDelete);
        var overrides = new[] { Override(AppRoles.Manager, Permissions.FinanceDelete, granted: true) };

        PermissionService.Effective(AppRoles.Manager, Permissions.FinanceDelete, overrides).Should().BeTrue(
            "the grant is for Manager and must take effect for Manager");
        PermissionService.Effective(AppRoles.Receptionist, Permissions.FinanceDelete, overrides).Should().BeFalse(
            "the grant named Manager, not Receptionist, and must not leak across roles");
    }

    [Fact]
    public void Effective_OverrideForADifferentPermission_DoesNotAffectThisPermission()
    {
        var overrides = new[] { Override(AppRoles.Manager, Permissions.OrdersCancel, granted: false) };

        PermissionService.Effective(AppRoles.Manager, Permissions.OrdersCreate, overrides).Should().BeTrue(
            "the override targets a different permission and must not suppress an unrelated one");
    }

    // ───────────────────────────────────────────────────────────────
    // SystemAdmin: not customisable either (deliberately excluded from CustomisableRoles)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Effective_SystemAdminRole_IgnoresOverrides()
    {
        var overrides = new[] { Override(AppRoles.SystemAdmin, Permissions.ReportsView, granted: false) };

        PermissionService.Effective(AppRoles.SystemAdmin, Permissions.ReportsView, overrides).Should().BeTrue();
    }

    // ───────────────────────────────────────────────────────────────
    // Unknown or mis-cased role: both lookups are ordinal, so neither matches loosely.
    // ───────────────────────────────────────────────────────────────

    // Rule: a role name that isn't a recognised role has no permissions at all.
    // Why:  role names in a JWT claim come from the database, but a defensive default of "no
    //       access" for anything Defaults.TryGetValue can't find is what keeps an unrecognised
    //       or corrupted role claim from being treated as having every permission by accident.
    [Fact]
    public void Effective_UnknownRoleName_ReturnsFalse()
        => PermissionService.Effective("NotARealRole", Permissions.OrdersView, overrides: Array.Empty<TenantRolePermission>())
            .Should().BeFalse();

    // Rule: role name matching is exact (ordinal), not case-insensitive.
    // Why:  AppRoles.Manager is the literal string "Manager"; Defaults and CustomisableRoles are
    //       looked up with plain dictionary/HashSet access, which is ordinal by default. A claim
    //       spelled "manager" would silently resolve to zero permissions rather than Manager's —
    //       worth pinning explicitly so a future case-insensitive "helpful" change is deliberate.
    [Fact]
    public void Effective_DifferentlyCasedRoleName_DoesNotMatch()
        => PermissionService.Effective("manager", Permissions.OrdersCreate, overrides: Array.Empty<TenantRolePermission>())
            .Should().BeFalse();
    // ───────────────────────────────────────────────────────────────
    // A business's own roles ("c_…"): only what was granted, never Owner-only
    // ───────────────────────────────────────────────────────────────

    private const string ShopOwnRole = AppRoles.CustomPrefix + "6f1c0b8e2d4a4b7f9e3c5a1d2b8e7f60";

    // Rule: a business's own role holds a permission only when a grant row says so.
    // Why:  own roles have no defaults, so the grant rows are the whole definition of the role.
    [Fact]
    public void Effective_OwnRole_GrantedPermission_ReturnsTrue()
    {
        var overrides = new[] { Override(ShopOwnRole, Permissions.OrdersCreate, granted: true) };

        PermissionService.Effective(ShopOwnRole, Permissions.OrdersCreate, overrides).Should().BeTrue();
    }

    // Rule: with no grant row, a business's own role has nothing.
    // Why:  overrides are loaded per business, so this is also what another business's role
    //       name sees here: a role claim from elsewhere must resolve to no access at all.
    [Fact]
    public void Effective_OwnRole_WithoutGrantRow_ReturnsFalse()
        => PermissionService.Effective(ShopOwnRole, Permissions.OrdersView, overrides: Array.Empty<TenantRolePermission>())
            .Should().BeFalse();

    // Rule: a grant row of an Owner-only permission is ignored for a business's own role.
    // Why:  the same guard as for built-in roles; a hand-edited row must not make a custom
    //       role able to manage staff or roles.
    [Fact]
    public void Effective_OwnRole_NonDelegableGrantIsIgnored()
    {
        var overrides = new[] { Override(ShopOwnRole, Permissions.SettingsPermissions, granted: true) };

        PermissionService.Effective(ShopOwnRole, Permissions.SettingsPermissions, overrides).Should().BeFalse();
    }

    // Rule: a grant row for one own role does not leak to another own role.
    // Why:  rows are matched by exact role name.
    [Fact]
    public void Effective_OwnRole_GrantForAnotherRole_ReturnsFalse()
    {
        var overrides = new[] { Override(AppRoles.CustomPrefix + "aaaa", Permissions.OrdersCreate, granted: true) };

        PermissionService.Effective(ShopOwnRole, Permissions.OrdersCreate, overrides).Should().BeFalse();
    }

    // ───────────────────────────────────────────────────────────────
    // New built-in roles (2026-11)
    // ───────────────────────────────────────────────────────────────

    // Rule: the Master (head tailor) can assign and check work but never sees money.
    // Why:  that separation is the reason the role exists instead of making the master a Manager.
    [Theory]
    [InlineData(Permissions.ProductionAssign, true)]
    [InlineData(Permissions.ProductionQc, true)]
    [InlineData(Permissions.FinanceView, false)]
    [InlineData(Permissions.PaymentsRecord, false)]
    [InlineData(Permissions.UsersView, false)]
    public void Effective_Master_Defaults(string permission, bool expected)
        => PermissionService.Effective(AppRoles.Master, permission, overrides: Array.Empty<TenantRolePermission>())
            .Should().Be(expected);

    // Rule: production worker roles get exactly the karigar's default access.
    // Why:  the app opens the karigar screens for production work without the order list;
    //       a cutter or presser must land there and see no money.
    [Theory]
    [InlineData(AppRoles.Cutter)]
    [InlineData(AppRoles.KajButton)]
    [InlineData(AppRoles.Embroiderer)]
    [InlineData(AppRoles.Presser)]
    [InlineData(AppRoles.Helper)]
    public void Defaults_ProductionWorkerRoles_MatchKarigar(string role)
        => Permissions.Defaults[role].Should().BeEquivalentTo(Permissions.Defaults[AppRoles.Karigar]);

    // Rule: a new built-in role can be customised per business like the original three.
    [Fact]
    public void Effective_NewBuiltInRole_OverrideRevokesDefault()
    {
        var overrides = new[] { Override(AppRoles.Salesman, Permissions.OrdersCreate, granted: false) };

        PermissionService.Effective(AppRoles.Salesman, Permissions.OrdersCreate, overrides).Should().BeFalse();
    }
}
