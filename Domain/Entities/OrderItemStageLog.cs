using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Append-only log of stage work done on an order item by a karigar.
/// </summary>
public class OrderItemStageLog : BaseDBModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public Guid KarigarId { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public StageLogStatus Status { get; set; } = StageLogStatus.InProgress;
    public string? ReworkReason { get; set; }

    // Cutting-stage details (null for other stages)
    public decimal? FabricUsed { get; set; }
    public int? CutQuantity { get; set; }
    public string? DefectNotes { get; set; }
    public bool RecutRequired { get; set; }

    // Navigation
    public OrderItem OrderItem { get; set; } = null!;
    public User? Karigar { get; set; }
}
