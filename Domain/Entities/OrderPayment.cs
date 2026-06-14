using Domain.Enums;

namespace Domain.Entities;

public class OrderPayment : BaseDBModel
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Note { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Order Order { get; set; } = null!;
}
