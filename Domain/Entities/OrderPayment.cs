using Domain.Enums;

namespace Domain.Entities;

public class OrderPayment : BaseDBModel
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Note { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    /// <summary>True for a refund line. Refunds are stored as negative amounts; history is never edited.</summary>
    public bool IsRefund { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
}
