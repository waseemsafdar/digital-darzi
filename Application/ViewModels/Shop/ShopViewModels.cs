using Application.Common;
using Domain.Enums;

namespace Application.ViewModels.Shop;

// ── Create ─────────────────────────────────────────────────────────────────
public class CreateShopViewModel
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
public class UpdateShopViewModel
{
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
public class ShopDetailViewModel
{
    public Guid Id { get; set; }
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
    public DateTime CreatedOn { get; set; }
}
