using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Json;

/// <summary>
/// Normalises every inbound <see cref="DateTime"/> to <see cref="DateTimeKind.Utc"/>.
///
/// Npgsql maps <c>DateTime</c> to <c>timestamp with time zone</c> and rejects any value whose
/// Kind is not Utc. Without this, a payload carrying an offset ("2026-08-09T00:00:00+05:00" →
/// Kind=Local) or no zone at all ("2026-08-09" → Kind=Unspecified) fails at SaveChanges with a
/// raw driver error. Clients in a +05:00 market send both shapes routinely.
///
/// Offsets are converted to the equivalent UTC instant; zone-less values are taken to already be
/// UTC, which is the only sane reading for a bare calendar date.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ToUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

/// <summary>
/// Nullable counterpart. System.Text.Json does not apply a <c>JsonConverter&lt;DateTime&gt;</c>
/// to <c>DateTime?</c>, so both must be registered.
/// </summary>
public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : UtcDateTimeConverter.ToUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(UtcDateTimeConverter.ToUtc(value.Value));
        else writer.WriteNullValue();
    }
}
