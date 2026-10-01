using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.Orders;

/// <summary>
/// The measurements frozen onto an order item when the order is created. Version 2 stores the
/// field name and unit with every value so the snapshot stays readable after the template or
/// field is renamed or deleted. Version 1 (orders created before 2026-09) was a bare
/// <c>{ "fieldId": value }</c> object and is still read.
/// </summary>
public sealed class MeasurementSnapshot
{
    [JsonPropertyName("v")] public int Version { get; set; } = 2;
    [JsonPropertyName("templateId")] public Guid? TemplateId { get; set; }
    [JsonPropertyName("templateName")] public string? TemplateName { get; set; }
    [JsonPropertyName("profileId")] public Guid? ProfileId { get; set; }
    [JsonPropertyName("profileVersion")] public int? ProfileVersion { get; set; }
    [JsonPropertyName("values")] public List<Value> Values { get; set; } = new();

    public sealed class Value
    {
        [JsonPropertyName("fieldId")] public Guid FieldId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("unit")] public MeasurementUnit? Unit { get; set; }
        [JsonPropertyName("value")] public decimal? Number { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    public string Serialize() => JsonSerializer.Serialize(this);

    /// <summary>Numeric values keyed by field id — the legacy response shape.</summary>
    public Dictionary<string, decimal> ToLegacyDictionary()
        => Values.Where(v => v.Number.HasValue).ToDictionary(v => v.FieldId.ToString(), v => v.Number!.Value);

    /// <summary>Parses either format. Never throws: unreadable content yields an empty snapshot.</summary>
    public static MeasurementSnapshot Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new MeasurementSnapshot();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new MeasurementSnapshot();

            if (root.TryGetProperty("v", out var v) && v.ValueKind == JsonValueKind.Number)
                return JsonSerializer.Deserialize<MeasurementSnapshot>(json) ?? new MeasurementSnapshot();

            // Version 1: { "fieldId": number, ... }
            var legacy = new MeasurementSnapshot { Version = 1 };
            foreach (var prop in root.EnumerateObject())
            {
                if (!Guid.TryParse(prop.Name, out var fieldId)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var num))
                    legacy.Values.Add(new Value { FieldId = fieldId, Name = prop.Name, Number = num });
            }
            return legacy;
        }
        catch (JsonException)
        {
            return new MeasurementSnapshot();
        }
    }
}
