using Domain.Enums;

namespace Domain.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// Platform-level records. These are NOT tenant-filtered by the global query
// filters; every query against them supplies its own predicate.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Stored refresh token (only the SHA-256 hash is persisted).</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                 // → domain Users.Id
    public Guid TenantId { get; set; }
    public Guid ShopId { get; set; }                 // branch the token was issued for
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt == null && ExpiresAt > utcNow;
}

/// <summary>One-time code for password / PIN reset. Only the hash is stored.</summary>
public class PasswordResetCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int Attempts { get; set; }
}

/// <summary>A sellable SaaS plan with usage limits. Managed by SystemAdmin.</summary>
public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "PKR";
    /// <summary>Null = unlimited.</summary>
    public int? MaxBranches { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxOrdersPerMonth { get; set; }
    public int TrialDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    /// <summary>The plan a shop falls back to when its trial or paid period ends (instead of being locked out).</summary>
    public bool IsFreeTier { get; set; }
    /// <summary>Order photos (design and fabric) can be uploaded.</summary>
    public bool IncludesPhotos { get; set; } = true;
    /// <summary>Revenue, customer and staff reports are available.</summary>
    public bool IncludesReports { get; set; } = true;
}

/// <summary>Invoice the platform raises to a tenant for a subscription period.</summary>
public class TenantInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ShopId { get; set; }
    public Guid? PlanId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PKR";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public DateTime CreatedOn { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }

    /// <summary>Raised by the Owner from the app (choose a plan), rather than by the platform admin.</summary>
    public bool IsSelfService { get; set; }

    // Proof of payment sent by the Owner, checked by the platform admin.
    public InvoiceReviewStatus ReviewStatus { get; set; } = InvoiceReviewStatus.None;
    public string? ProofStorageKey { get; set; }
    public string? ProofContentType { get; set; }
    public string? ProofFileName { get; set; }
    /// <summary>Transaction id the Owner typed from the JazzCash / Easypaisa / bank receipt.</summary>
    public string? ProofReference { get; set; }
    /// <summary>How the Owner says they paid, e.g. "JazzCash".</summary>
    public string? ProofPaidVia { get; set; }
    public string? ProofNote { get; set; }
    public DateTime? ProofSubmittedAt { get; set; }
    public string? RejectReason { get; set; }
}

/// <summary>Per-tenant override of the default role → permission map.</summary>
public class TenantRolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public DateTime UpdatedOn { get; set; }
    public Guid UpdatedBy { get; set; }
}

/// <summary>
/// A business's settings for one sign-in role. For a built-in role it stores only a rename or the
/// hidden flag. For the business's own role (<see cref="IsCustom"/>) it is the role itself; its
/// permissions are <see cref="TenantRolePermission"/> rows with the same RoleName.
/// </summary>
public class ShopRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    /// <summary>The sign-in role name (built-in name, or "c_…" for the business's own role).</summary>
    public string RoleName { get; set; } = string.Empty;
    public Guid IdentityRoleId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCustom { get; set; }
    /// <summary>Not offered when adding staff. People who already hold it keep it.</summary>
    public bool IsHidden { get; set; }
    public DateTime CreatedOn { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedOn { get; set; }
    public Guid UpdatedBy { get; set; }
}

/// <summary>Append-only audit trail written by the DbContext for whitelisted entities.</summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;      // Created / Updated / Deleted / custom verb
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? OldValues { get; set; }                  // JSONB
    public string? NewValues { get; set; }                  // JSONB
    public DateTime CreatedOn { get; set; }
}
