using Application.Common;
using Application.ViewModels.Common;
using Domain.Enums;

namespace Application.ViewModels.Shop;

// ── Create ─────────────────────────────────────────────────────────────────
public class CreateShopViewModel : IBaseCrudViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "₨";
}

// ── Update ─────────────────────────────────────────────────────────────────
public class UpdateShopViewModel : IBaseCrudViewModel, IIdentification
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }
    public ActiveStatus? ActiveStatus { get; set; }
}

// ── List ───────────────────────────────────────────────────────────────────
public class ShopListViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Phone { get; set; }
    public ActiveStatus ActiveStatus { get; set; }
    public DateTime CreatedOn { get; set; }
}

// ── Detail ─────────────────────────────────────────────────────────────────
public class ShopDetailViewModel : IBaseCrudViewModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "₨";
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public ActiveStatus ActiveStatus { get; set; }
    
    // Subscription Data
    public SubscriptionStatus? Status { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    public string? SubscriptionPlanName { get; set; }

    public DateTime CreatedOn { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

// ── Admin Subscription Update ──────────────────────────────────────────────
public class UpdateShopSubscriptionRequest
{
    public SubscriptionStatus Status { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    public string? SubscriptionPlanName { get; set; }
}
