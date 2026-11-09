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
    public bool IsFreeTier { get; set; }
    public bool IncludesPhotos { get; set; }
    public bool IncludesReports { get; set; }
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
    public bool IsFreeTier { get; set; }
    public bool IncludesPhotos { get; set; } = true;
    public bool IncludesReports { get; set; } = true;
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
    public bool IsSelfService { get; set; }
    public InvoiceReviewStatus ReviewStatus { get; set; }
    public bool HasProof { get; set; }
    public string? ProofReference { get; set; }
    public string? ProofPaidVia { get; set; }
    public string? ProofNote { get; set; }
    public DateTime? ProofSubmittedAt { get; set; }
    public string? RejectReason { get; set; }
}

/// <summary>Where a business stands: on trial, paid, dropped to the free plan, or locked by the platform.</summary>
public static class SubscriptionStates
{
    public const string Legacy  = "legacy";    // no subscription data: never limited or locked
    public const string Trial   = "trial";
    public const string Active  = "active";
    public const string Free    = "free";      // trial or paid period ended, running on the free plan
    public const string Blocked = "blocked";   // suspended / cancelled, or ended with no free plan to fall back to
}

public class CheckoutViewModel
{
    public Guid PlanId { get; set; }
    /// <summary>1, 6 or 12.</summary>
    public int Months { get; set; } = 1;
}

public class RejectProofViewModel
{
    public string Reason { get; set; } = string.Empty;
}

public class BillingPeriodOptionViewModel
{
    public int Months { get; set; }
    public int DiscountPercent { get; set; }
}

public class PaymentAccountViewModel
{
    /// <summary>e.g. "JazzCash", "Easypaisa", "Bank transfer".</summary>
    public string Method { get; set; } = string.Empty;
    public string AccountTitle { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    /// <summary>Bank name or IBAN line, when it helps.</summary>
    public string? Details { get; set; }
}

public class BillingOptionsViewModel
{
    public List<SubscriptionPlanViewModel> Plans { get; set; } = new();
    public List<BillingPeriodOptionViewModel> Periods { get; set; } = new();
    public List<PaymentAccountViewModel> PaymentAccounts { get; set; } = new();
    public string? Instructions { get; set; }
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

    /// <summary>One of <see cref="SubscriptionStates"/>.</summary>
    public string State { get; set; } = SubscriptionStates.Legacy;
    /// <summary>The plan whose limits apply now (the free plan once a trial or paid period has ended).</summary>
    public SubscriptionPlanViewModel? EffectivePlan { get; set; }
    /// <summary>Whole days left on the trial or paid period; null when it does not end.</summary>
    public int? DaysLeft { get; set; }
    /// <summary>A payment the Owner sent that the platform has not checked yet.</summary>
    public bool PaymentAwaitingReview { get; set; }
}
