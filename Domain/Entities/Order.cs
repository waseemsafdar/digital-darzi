using Domain.Enums;

namespace Domain.Entities;

public class Order : BaseDBModel
{
    public Guid ShopId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
    public DateTime DeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }

    // Financials
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; } = 0;
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; } = 0;
    public decimal BalanceDue { get; set; } = 0;

    // Cancellation
    public string? CancellationReason { get; set; }
    public CancellationStage? CancellationStage { get; set; }
    public decimal RefundAmount { get; set; } = 0;
    public RefundStatus? RefundStatus { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();
    public ICollection<OrderAlteration> Alterations { get; set; } = new List<OrderAlteration>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
