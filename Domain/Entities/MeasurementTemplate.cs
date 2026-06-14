using Domain.Enums;

namespace Domain.Entities;

public class MeasurementTemplate : BaseDBModel
{
    public Guid ShopId { get; set; }
    // TenantId = Guid.Empty → system seed record
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GarmentType? GarmentType { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsSystemTemplate { get; set; } = false;
    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<TemplateField> TemplateFields { get; set; } = new List<TemplateField>();
}
