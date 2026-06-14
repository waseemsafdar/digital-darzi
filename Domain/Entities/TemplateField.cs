namespace Domain.Entities;

/// <summary>
/// Junction table: which MeasurementFields belong to which MeasurementTemplate.
/// </summary>
public class TemplateField : BaseDBModel
{
    public Guid TemplateId { get; set; }
    public Guid MeasurementFieldId { get; set; }
    public bool IsRequired { get; set; } = false;   // overrides field-level default
    public int SortOrder { get; set; }

    // Navigation
    public MeasurementTemplate Template { get; set; } = null!;
    public MeasurementField MeasurementField { get; set; } = null!;
}
