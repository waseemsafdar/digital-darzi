using Domain.Enums;

namespace Domain.Entities;

public class Customer : BaseDBModel
{
    public Guid ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
    public int TotalOrders { get; set; } = 0;
    public decimal TotalSpend { get; set; } = 0;
    public int LoyaltyPoints { get; set; } = 0;
    public string LoyaltyTier { get; set; } = "Bronze";

    // Navigation
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<MeasurementProfile> MeasurementProfiles { get; set; } = new List<MeasurementProfile>();
}
