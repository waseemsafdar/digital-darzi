using Domain.Enums;

namespace Application.ViewModels.Admin;

public class SubscriptionPlanViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "PKR";
    public int? MaxBranches { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxOrdersPerMonth { get; set; }
    public int TrialDays { get; set; }
    public bool IsActive { get; set; }
}

public class SaveSubscriptionPlanViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "PKR";
    public int? MaxBranches { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxOrdersPerMonth { get; set; }
    public int TrialDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;
}

public class AssignPlanViewModel
{
    public Guid PlanId { get; set; }
}

public class CreateInvoiceViewModel
{
    public Guid ShopId { get; set; }
    /// <summary>Billing months covered (1–12). The period starts at the current subscription end, or today.</summary>
    public int Months { get; set; } = 1;
    /// <summary>Override the plan price (e.g. a discount). Null = plan price × months.</summary>
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
}

public class MarkInvoicePaidViewModel
{
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? PaymentReference { get; set; }
}

public class TenantInvoiceViewModel
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid ShopId { get; set; }
    public string? ShopName { get; set; }
    public string? PlanName { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PKR";
    public InvoiceStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

public class TenantSubscriptionViewModel
{
    public Guid TenantId { get; set; }
    public Guid GoverningShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public SubscriptionStatus? Status { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    public SubscriptionPlanViewModel? Plan { get; set; }
    public int Branches { get; set; }
    public int ActiveUsers { get; set; }
    public int OrdersThisMonth { get; set; }
    public decimal OutstandingInvoices { get; set; }
}
