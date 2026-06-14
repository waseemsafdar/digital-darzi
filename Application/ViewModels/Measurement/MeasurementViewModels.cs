using Domain.Enums;

namespace Application.ViewModels.Measurement;

// ── Field ───────────────────────────────────────────────────────────────────
public class CreateMeasurementFieldViewModel
{
    public string Name { get; set; } = string.Empty;
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Inch;
    public int SortOrder { get; set; }
}

public class UpdateMeasurementFieldViewModel
{
    public string? Name { get; set; }
    public MeasurementUnit? Unit { get; set; }
    public int? SortOrder { get; set; }
}

public class MeasurementFieldViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MeasurementUnit Unit { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystemField { get; set; }
}

// ── Template ─────────────────────────────────────────────────────────────────
public class CreateTemplateViewModel
{
    public string Name { get; set; } = string.Empty;
    public GarmentType? GarmentType { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
    public List<TemplateFieldItemViewModel> Fields { get; set; } = new();
}

public class UpdateTemplateViewModel
{
    public string? Name { get; set; }
    public GarmentType? GarmentType { get; set; }
    public bool? IsDefault { get; set; }
    public int? DisplayOrder { get; set; }
    public List<TemplateFieldItemViewModel>? Fields { get; set; }
}

public class TemplateFieldItemViewModel
{
    public Guid MeasurementFieldId { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

public class TemplateListViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GarmentType? GarmentType { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSystemTemplate { get; set; }
    public int DisplayOrder { get; set; }
    public int FieldCount { get; set; }
}

public class TemplateDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GarmentType? GarmentType { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSystemTemplate { get; set; }
    public int DisplayOrder { get; set; }
    public List<TemplateFieldDetailViewModel> Fields { get; set; } = new();
}

public class TemplateFieldDetailViewModel
{
    public Guid MeasurementFieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public MeasurementUnit Unit { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

// ── Profile ───────────────────────────────────────────────────────────────────
public class SaveMeasurementProfileViewModel
{
    public Guid CustomerId { get; set; }
    public Guid TemplateId { get; set; }
    public string? ProfileName { get; set; }        // e.g. "Suit Measurements"
    public Dictionary<Guid, decimal> Values { get; set; } = new(); // FieldId → value
    public string? Notes { get; set; }
}

public class MeasurementProfileListViewModel
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public GarmentType? GarmentType { get; set; }
    public DateTime UpdatedOn { get; set; }
}

public class MeasurementProfileDetailViewModel
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public GarmentType? GarmentType { get; set; }
    public Dictionary<string, decimal> Values { get; set; } = new(); // FieldName → value
    public string? Notes { get; set; }
    public DateTime UpdatedOn { get; set; }
}
