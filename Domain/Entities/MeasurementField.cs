using Domain.Enums;

namespace Domain.Entities;

public class MeasurementField : BaseDBModel
{
    public Guid ShopId { get; set; }
    // TenantId = Guid.Empty → system seed record shared across all tenants
    public string Name { get; set; } = string.Empty;
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Inch;
    public bool IsRequired { get; set; } = false;
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<TemplateField> TemplateFields { get; set; } = new List<TemplateField>();
}
