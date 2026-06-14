using Domain.Enums;

namespace Domain.Entities;

public class OrderAlteration : BaseDBModel
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AdditionalCharge { get; set; } = 0;
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
