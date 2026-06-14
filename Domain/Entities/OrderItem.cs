using Domain.Enums;

namespace Domain.Entities;

public class OrderItem : BaseDBModel
{
    public Guid OrderId { get; set; }
    public GarmentType GarmentType { get; set; }
    public Guid? MeasurementProfileId { get; set; }
    public string? MeasurementSnapshot { get; set; }   // JSONB: {fieldId: value, ...}
    public string? FabricDescription { get; set; }
    public string? FabricColor { get; set; }
    public string? StyleNotes { get; set; }
    public decimal Price { get; set; }
    public int Qty { get; set; } = 1;
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;
    public bool IsDelivered { get; set; } = false;
    public DateTime? DeliveredOn { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public MeasurementProfile? MeasurementProfile { get; set; }
    public ICollection<OrderItemStageAssignment> StageAssignments { get; set; } = new List<OrderItemStageAssignment>();
    public ICollection<OrderItemStageLog> StageLogs { get; set; } = new List<OrderItemStageLog>();
    public ICollection<OrderAlteration> Alterations { get; set; } = new List<OrderAlteration>();
}
