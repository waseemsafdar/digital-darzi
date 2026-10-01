using Domain.Enums;

namespace Domain.Entities;

public class Shop : BaseDBModel
{
    /// <summary>Default IANA time zone for a new shop. Used for "today", check-in times and calendar dates.</summary>
    public const string DefaultTimeZone = "Asia/Karachi";

    public Guid? OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "₨";
    public string TimeZone { get; set; } = DefaultTimeZone;
    public bool NotificationsEnabled { get; set; } = true;

    // Subscription & Trial Tracking
    public SubscriptionStatus? Status { get; set; }
    public DateTime? TrialEndsAt { get; set; }

    // Paid Subscription Tracking
    public DateTime? SubscriptionEndsAt { get; set; }
    public string? SubscriptionPlanName { get; set; }
    public Guid? SubscriptionPlanId { get; set; }

    // Navigation
    public User? Owner { get; set; }
}
