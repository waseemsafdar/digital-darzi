namespace Domain.Entities;

/// <summary>
/// Customer's saved measurement card for a specific template.
/// FieldValuesJson stores { "fieldId-guid": 38.5, "fieldId-guid2": 34.0 }.
/// </summary>
public class MeasurementProfile : BaseDBModel
{
    public Guid CustomerId { get; set; }
    public Guid TemplateId { get; set; }
    public string? ProfileName { get; set; }         // e.g. "Suit – June 2025"
    public string? FieldValuesJson { get; set; }     // JSONB: {fieldId → decimal}
    public string? TextValuesJson { get; set; }      // JSONB: {fieldId → string} for text-type fields
    public int CurrentVersion { get; set; } = 1;
    public string? Notes { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public MeasurementTemplate Template { get; set; } = null!;
    public ICollection<MeasurementProfileVersion> Versions { get; set; } = new List<MeasurementProfileVersion>();
}
