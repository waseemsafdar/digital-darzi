using Application.Orders;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DigitalDarzi.UnitTests.Orders;

public class MeasurementSnapshotTests
{
    private static readonly Guid ChestFieldId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid NotesFieldId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ───────────────────────────────────────────────────────────────
    // Parse — version 2 (current) format
    // ───────────────────────────────────────────────────────────────

    private static readonly Guid TemplateId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Rule: version 2 keeps the field NAME alongside the value, not just the id.
    // Why:  this is the entire point of versioning the snapshot — if a field is later renamed
    //       or deleted, an order placed years ago must still show "Chest: 40" on its receipt
    //       instead of a bare, meaningless GUID.
    [Fact]
    public void Parse_Version2Json_ReadsNamedValuesWithUnit()
    {
        var snapshot = new MeasurementSnapshot
        {
            TemplateId = TemplateId,
            TemplateName = "Gents Shalwar Kameez",
            Values = { new MeasurementSnapshot.Value { FieldId = ChestFieldId, Name = "Chest", Unit = MeasurementUnit.Inch, Number = 40m } }
        };
        var json = snapshot.Serialize();

        var parsed = MeasurementSnapshot.Parse(json);

        parsed.Version.Should().Be(2);
        parsed.TemplateName.Should().Be("Gents Shalwar Kameez");
        var value = parsed.Values.Should().ContainSingle().Subject;
        value.FieldId.Should().Be(ChestFieldId);
        value.Name.Should().Be("Chest");
        value.Number.Should().Be(40m);
        value.Unit.Should().Be(MeasurementUnit.Inch);
    }

    [Fact]
    public void Parse_Version2Json_ReadsTextValues()
    {
        var snapshot = new MeasurementSnapshot
        {
            Values = { new MeasurementSnapshot.Value { FieldId = NotesFieldId, Name = "Fit preference", Text = "Loose fit at the waist" } }
        };

        var parsed = MeasurementSnapshot.Parse(snapshot.Serialize());

        parsed.Values.Single().Text.Should().Be("Loose fit at the waist");
    }

    // ───────────────────────────────────────────────────────────────
    // Parse — version 1 (legacy, pre-2026-09) format: a bare {fieldId: number} object
    // ───────────────────────────────────────────────────────────────

    // Rule: orders created before the versioned snapshot existed still store the bare legacy shape.
    // Why:  every order placed before this release must keep rendering its measurements correctly;
    //       misreading old orders as empty would erase a customer's fitting history from their record.
    [Fact]
    public void Parse_LegacyBareObjectJson_IsReadAsVersion1()
    {
        var legacyJson = $$"""{"{{ChestFieldId}}": 38.5}""";

        var parsed = MeasurementSnapshot.Parse(legacyJson);

        parsed.Version.Should().Be(1);
        parsed.Values.Should().ContainSingle(v => v.FieldId == ChestFieldId && v.Number == 38.5m);
    }

    [Fact]
    public void Parse_LegacyJson_FieldNameFallsBackToRawIdString()
        // Version 1 payloads carried no field name at all. Parse() fills Name with the raw id
        // string as a placeholder (rather than leaving it blank), and the caller is responsible
        // for resolving the real name from the field id separately when one is needed.
        => MeasurementSnapshot.Parse($$"""{"{{ChestFieldId}}": 38.5}""")
            .Values.Single().Name.Should().Be(ChestFieldId.ToString());

    [Fact]
    public void ToLegacyDictionary_RoundTripsNumericValuesByFieldId()
    {
        var snapshot = new MeasurementSnapshot
        {
            Values =
            {
                new MeasurementSnapshot.Value { FieldId = ChestFieldId, Number = 40m },
                new MeasurementSnapshot.Value { FieldId = NotesFieldId, Text = "text-only, excluded from legacy dictionary" }
            }
        };

        var legacy = snapshot.ToLegacyDictionary();

        legacy.Should().ContainKey(ChestFieldId.ToString()).WhoseValue.Should().Be(40m);
        legacy.Should().NotContainKey(NotesFieldId.ToString(), "text values have no place in the numeric-only legacy shape");
    }

    // ───────────────────────────────────────────────────────────────
    // Parse — defensive handling of unreadable content
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmpty_ReturnsEmptySnapshot(string? input)
        => MeasurementSnapshot.Parse(input).Values.Should().BeEmpty();

    // Rule: malformed JSON must never throw and abort an order/receipt render.
    // Why:  the snapshot is written by OrderService.BuildSnapshotAsync from the shop's own
    //       measurement fields, so a corrupt row would only come from a bug or a hand edit of
    //       the database — but if that ever happens, one bad row must not take down the whole
    //       order-detail endpoint for that shop while every other order keeps rendering fine.
    [Fact]
    public void Parse_MalformedJson_DoesNotThrow()
    {
        Action act = () => MeasurementSnapshot.Parse("{not valid json");
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsEmptySnapshot()
        => MeasurementSnapshot.Parse("{not valid json").Values.Should().BeEmpty();

    [Fact]
    public void Parse_JsonArray_ReturnsEmptySnapshot()
        // Neither legacy nor v2 shape is an array; must degrade gracefully, not throw.
        => MeasurementSnapshot.Parse("[1,2,3]").Values.Should().BeEmpty();

    [Fact]
    public void Parse_LegacyJsonWithNonGuidKey_SkipsUnparsableKeys()
        => MeasurementSnapshot.Parse("""{"not-a-guid": 10, "also bad": "x"}""").Values.Should().BeEmpty();

    // Rule: a legacy-shaped GUID key whose value is not a JSON number is skipped, not read.
    // Why:  TryGetDecimal throws InvalidOperationException on a non-Number JsonElement, and that
    //       exception type is not a JsonException — the outer catch would not stop it. The
    //       ValueKind == Number check has to run and short-circuit BEFORE TryGetDecimal is ever
    //       called, or a single string-valued legacy field would crash order-detail with an
    //       unhandled exception instead of degrading gracefully like every other bad row here.
    [Fact]
    public void Parse_LegacyJsonWithGuidKeyButStringValue_SkipsItWithoutThrowing()
    {
        var json = $$"""{"{{ChestFieldId}}": "not a number"}""";

        Action act = () => MeasurementSnapshot.Parse(json);
        act.Should().NotThrow();
        MeasurementSnapshot.Parse(json).Values.Should().BeEmpty();
    }
}
