using System.Text.Json;

namespace Domain.Entities;

/// <summary>
/// Immutable snapshot of measurements frozen at order creation.
/// Never updated even if the profile or template changes later.
/// </summary>
public class OrderMeasurementSnapshot : BaseDBModel
{
    public Guid OrderItemId { get; set; }
    public Guid? ProfileId { get; set; }            // → MeasurementProfiles.Id (source)
    public Guid? TemplateId { get; set; }           // → MeasurementTemplates.Id
    public JsonDocument? SnapshotJson { get; set; } // FROZEN: { templateName, measuredOn, fields:[...] }
    public string? AlterationNotes { get; set; }    // what differs from profile for this order

    // Navigation
    public OrderItem OrderItem { get; set; } = null!;
}
