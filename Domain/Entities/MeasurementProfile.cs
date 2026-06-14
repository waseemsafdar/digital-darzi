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
    public string? Notes { get; set; }
    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

    // Navigation
    public Customer Customer { get; set; } = null!;
    public MeasurementTemplate Template { get; set; } = null!;
    public ICollection<TemplateField> TemplateFields => Template?.TemplateFields ?? new List<TemplateField>();
}
