using Domain.Enums;

namespace Domain.Entities;

public class Shop : BaseDBModel
{
    public Guid? OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "₨";

    // Subscription & Trial Tracking
    public SubscriptionStatus? Status { get; set; }
    public DateTime? TrialEndsAt { get; set; }

    // Paid Subscription Tracking
    public DateTime? SubscriptionEndsAt { get; set; }
    public string? SubscriptionPlanName { get; set; }

    // Navigation
    public User? Owner { get; set; }
}
