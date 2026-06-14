using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Append-only status audit trail. Every forward and backward status change is logged.
/// IsReversal = true when status goes backward — Notes are mandatory in that case.
/// </summary>
public class OrderStatusHistory : BaseDBModel
{
    public Guid OrderId { get; set; }
    public Guid? OrderItemId { get; set; }           // null = whole-order status change
    public Guid ChangedBy { get; set; }              // → Users.Id
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public bool IsReversal { get; set; } = false;    // true when status goes backward
    public Guid? AssignedFrom { get; set; }          // for reassignment logs
    public Guid? AssignedTo { get; set; }            // for reassignment logs
    public DateTime ChangedOn { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }               // mandatory when IsReversal = true

    // Navigation
    public Order Order { get; set; } = null!;
}
