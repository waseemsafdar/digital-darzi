using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Pre-plan which karigar handles which production stage of an order item.
/// </summary>
public class OrderItemStageAssignment : BaseDBModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    public decimal? StagePrice { get; set; }      // amount paid to karigar for this stage
    public int EstimatedDays { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public OrderItem OrderItem { get; set; } = null!;
    public User? AssignedKarigar { get; set; }
}
