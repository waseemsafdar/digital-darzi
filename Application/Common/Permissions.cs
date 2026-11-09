namespace Application.Common;

/// <summary>
/// Named permissions checked by the API. Controllers declare the permission they need; the
/// default role → permission map below reproduces the role rules of the frontend guide, and an
/// Owner may grant or revoke delegable permissions for Manager / Receptionist / Karigar per tenant.
/// </summary>
public static class Permissions
{
    public const string OrdersView              = "orders.view";
    public const string OrdersList              = "orders.list";
    public const string OrdersCreate            = "orders.create";
    public const string OrdersEdit              = "orders.edit";
    public const string OrdersStatus            = "orders.status";
    public const string OrdersCancel            = "orders.cancel";
    public const string OrdersDeliver           = "orders.deliver";
    public const string OrdersDeliverWithBalance = "orders.deliver_with_balance";
    public const string PaymentsRecord          = "payments.record";
    public const string PaymentsRefund          = "payments.refund";
    public const string AlterationsManage       = "alterations.manage";
    public const string ProductionAssign        = "production.assign";
    public const string ProductionWork          = "production.work";
    public const string ProductionQc            = "production.qc";
    public const string CustomersView           = "customers.view";
    public const string CustomersEdit           = "customers.edit";
    public const string CustomersDelete         = "customers.delete";
    public const string MeasurementsView         = "measurements.view";
    public const string MeasurementsEdit        = "measurements.edit";
    public const string MeasurementsConfigure   = "measurements.configure";
    public const string UsersView               = "users.view";
    public const string UsersEdit               = "users.edit";
    public const string UsersManage             = "users.manage";
    public const string ShopsManage             = "shops.manage";
    public const string ShopsEdit               = "shops.edit";
    public const string FinanceView             = "finance.view";
    public const string FinanceManage           = "finance.manage";
    public const string FinanceDelete           = "finance.delete";
    public const string ReportsView             = "reports.view";
    public const string ReportsTenantWide       = "reports.tenant_wide";
    public const string KarigarViewAll          = "karigar.view_all";
    public const string KarigarPayouts          = "karigar.payouts";
    public const string InventoryView           = "inventory.view";
    public const string InventoryManage         = "inventory.manage";
    public const string CashManage              = "cash.manage";
    public const string NotificationsManage     = "notifications.manage";
    public const string AuditView               = "audit.view";
    public const string SettingsPermissions     = "settings.permissions";
    public const string BillingView             = "billing.view";

    /// <summary>Permissions that only the Owner may ever hold; they cannot be delegated.</summary>
    public static readonly IReadOnlySet<string> NonDelegable = new HashSet<string>
    {
        SettingsPermissions, UsersManage, BillingView, ShopsManage, ReportsTenantWide
    };

    /// <summary>Roles whose permissions an Owner may customise.</summary>
    public static readonly IReadOnlySet<string> CustomisableRoles = new HashSet<string>
    {
        AppRoles.Manager, AppRoles.Receptionist, AppRoles.Karigar,
        AppRoles.Master, AppRoles.Cutter, AppRoles.KajButton, AppRoles.Embroiderer, AppRoles.Presser,
        AppRoles.Helper, AppRoles.Salesman, AppRoles.Munshi, AppRoles.Delivery
    };

    /// <summary>What every production worker role gets: their own jobs, in the karigar app.</summary>
    private static HashSet<string> ProductionWorker() => new() { OrdersView, ProductionWork, MeasurementsView };

    private static readonly string[] All =
    {
        OrdersView, OrdersList, OrdersCreate, OrdersEdit, OrdersStatus, OrdersCancel, OrdersDeliver,
        OrdersDeliverWithBalance, PaymentsRecord, PaymentsRefund, AlterationsManage,
        ProductionAssign, ProductionWork, ProductionQc, CustomersView, CustomersEdit,
        CustomersDelete, MeasurementsView, MeasurementsEdit, MeasurementsConfigure, UsersView,
        UsersEdit, UsersManage, ShopsManage, ShopsEdit, FinanceView, FinanceManage, FinanceDelete,
        ReportsView, ReportsTenantWide, KarigarViewAll, KarigarPayouts, InventoryView,
        InventoryManage, CashManage, NotificationsManage, AuditView, SettingsPermissions, BillingView
    };

    public static IReadOnlyList<string> AllPermissions => All;

    /// <summary>Default grants per role.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Defaults =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [AppRoles.Owner] = new HashSet<string>(All),

            [AppRoles.Manager] = new HashSet<string>
            {
                OrdersView, OrdersList, OrdersCreate, OrdersEdit, OrdersStatus, OrdersCancel, OrdersDeliver,
                OrdersDeliverWithBalance, PaymentsRecord, PaymentsRefund, AlterationsManage,
                ProductionAssign, ProductionWork, ProductionQc, CustomersView, CustomersEdit,
                CustomersDelete, MeasurementsView, MeasurementsEdit, MeasurementsConfigure,
                UsersView, UsersEdit, ShopsEdit, FinanceView, FinanceManage, ReportsView,
                KarigarViewAll, KarigarPayouts, InventoryView, InventoryManage, CashManage,
                NotificationsManage, AuditView
            },

            [AppRoles.Receptionist] = new HashSet<string>
            {
                OrdersView, OrdersList, OrdersDeliver, PaymentsRecord, CustomersView,
                CustomersEdit, MeasurementsView, MeasurementsEdit, InventoryView, CashManage
            },

            [AppRoles.Karigar] = new HashSet<string>
            {
                OrdersView, ProductionWork, MeasurementsView
            },

            // Head tailor: runs the workshop floor but never sees money or staff.
            [AppRoles.Master] = new HashSet<string>
            {
                OrdersView, OrdersList, OrdersEdit, OrdersStatus, AlterationsManage,
                ProductionAssign, ProductionWork, ProductionQc, CustomersView,
                MeasurementsView, MeasurementsEdit, MeasurementsConfigure, KarigarViewAll
            },

            [AppRoles.Cutter]      = ProductionWorker(),
            [AppRoles.KajButton]   = ProductionWorker(),
            [AppRoles.Embroiderer] = ProductionWorker(),
            [AppRoles.Presser]     = ProductionWorker(),
            [AppRoles.Helper]      = ProductionWorker(),

            // Counter: the receptionist plus taking and editing orders.
            [AppRoles.Salesman] = new HashSet<string>
            {
                OrdersView, OrdersList, OrdersCreate, OrdersEdit, OrdersDeliver, PaymentsRecord,
                CustomersView, CustomersEdit, MeasurementsView, MeasurementsEdit, InventoryView, CashManage
            },

            // Accounts: money in and out, no order or production changes.
            [AppRoles.Munshi] = new HashSet<string>
            {
                OrdersView, OrdersList, PaymentsRecord, CustomersView, FinanceView, FinanceManage,
                ReportsView, KarigarViewAll, KarigarPayouts, InventoryView, CashManage
            },

            [AppRoles.Delivery] = new HashSet<string>
            {
                OrdersView, OrdersList, OrdersDeliver, PaymentsRecord, CustomersView
            },

            // Platform operator: keeps the cross-tenant reach it had before (reports, karigar
            // overview, shop administration) but no day-to-day shop operations.
            [AppRoles.SystemAdmin] = new HashSet<string>
            {
                ReportsView, KarigarViewAll, ShopsManage, ShopsEdit
            }
        };

    public static bool IsKnown(string permission) => Array.IndexOf(All, permission) >= 0;
}
