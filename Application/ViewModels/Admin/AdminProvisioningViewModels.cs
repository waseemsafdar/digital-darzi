using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Admin;

// ── Step 1: Provision Owner ─────────────────────────────────────────────────

public class ProvisionOwnerRequest
{
    /// <summary>Full name of the owner.</summary>
    [Required]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Email address — used for email+password login.</summary>
    public string? Email { get; set; }

    /// <summary>Password — required if Email is provided.</summary>
    public string? Password { get; set; }

    /// <summary>Phone number — used for phone+PIN login.</summary>
    public string? Phone { get; set; }

    /// <summary>4-digit PIN — required if Phone is provided (and no email).</summary>
    public string? Passcode { get; set; }
}

public class ProvisionOwnerResult
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

// ── Step 2: Provision Shop ──────────────────────────────────────────────────

public class ProvisionShopRequest
{
    /// <summary>Domain User ID of the owner (returned from Step 1).</summary>
    public Guid UserId { get; set; }

    [Required]
    [MinLength(2, ErrorMessage = "Shop name must be at least 2 characters.")]
    public string ShopName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "₨";
}

public class ProvisionShopResult
{
    public Guid ShopId { get; set; }
    public Guid TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

// ── Step 3: Provision Subscription ─────────────────────────────────────────

public class ProvisionSubscriptionRequest
{
    /// <summary>Shop ID to assign the subscription to.</summary>
    public Guid ShopId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;

    /// <summary>Number of trial days — used when Status = Trial.</summary>
    public int TrialDays { get; set; } = 14;

    /// <summary>Hard expiry date — used when Status = Active.</summary>
    public DateTime? SubscriptionEndsAt { get; set; }

    public string? PlanName { get; set; }
}

// ── Admin Management Requests ───────────────────────────────────────────────

public class ExtendSubscriptionRequest
{
    /// <summary>Number of days to add to the current trial/subscription end date.</summary>
    public int Days { get; set; }
}

public class UpdateUserRoleRequest
{
    /// <summary>New role name to assign. E.g. "Owner", "Manager".</summary>
    public string NewRole { get; set; } = string.Empty;
}
