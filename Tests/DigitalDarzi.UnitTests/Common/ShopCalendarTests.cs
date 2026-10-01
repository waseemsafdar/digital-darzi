using Application.Common;
using FluentAssertions;
using Xunit;

namespace DigitalDarzi.UnitTests.Common;

public class ShopCalendarTests
{
    private static readonly TimeZoneInfo Karachi = ShopCalendar.ResolveTimeZone("Asia/Karachi"); // UTC+5, no DST

    // ───────────────────────────────────────────────────────────────
    // ResolveTimeZone
    // ───────────────────────────────────────────────────────────────

    // Rule: a known id resolves to THAT zone, not merely to something with a plausible offset.
    // Why:  Asia/Karachi is also this class's fallback zone, so asserting +5h for a
    //       "Asia/Karachi" request would pass even if the requested id were ignored entirely
    //       and every shop silently got the fallback. Asia/Dubai (+4h, no DST) is deliberately
    //       a different offset from the +5h fallback, so this can only pass if the id was honoured.
    [Fact]
    public void ResolveTimeZone_KnownIanaId_ResolvesToThatZone()
        => ShopCalendar.ResolveTimeZone("Asia/Dubai").BaseUtcOffset.Should().Be(TimeSpan.FromHours(4));

    // Rule: an unrecognised or missing time zone id falls back to Asia/Karachi, then UTC.
    // Why:  this is the whole fix for the "check-ins land on the wrong day" defect — a branch
    //       with a blank or bad TimeZone value must still get a sane local day, not silently
    //       revert to UTC and shift every check-in and due-date by five hours.
    [Fact]
    public void ResolveTimeZone_UnknownId_FallsBackToKarachi()
        => ShopCalendar.ResolveTimeZone("Definitely/NotAZone").BaseUtcOffset.Should().Be(TimeSpan.FromHours(5));

    [Fact]
    public void ResolveTimeZone_NullOrEmpty_FallsBackToKarachi()
    {
        ShopCalendar.ResolveTimeZone(null).BaseUtcOffset.Should().Be(TimeSpan.FromHours(5));
        ShopCalendar.ResolveTimeZone(string.Empty).BaseUtcOffset.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void IsKnownTimeZone_ValidId_ReturnsTrue()
        => ShopCalendar.IsKnownTimeZone("Asia/Karachi").Should().BeTrue();

    [Fact]
    public void IsKnownTimeZone_InvalidId_ReturnsFalse()
        => ShopCalendar.IsKnownTimeZone("Not/A/Real/Zone").Should().BeFalse();

    // ───────────────────────────────────────────────────────────────
    // Today — the exact defect this class exists to fix
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Today_LateEveningInKarachi_StillSameUtcDate_ReturnsThatDate()
    {
        // 2026-09-15 23:30 PKT = 2026-09-15 18:30 UTC (both same calendar day — the easy case,
        // which also passed under the old UTC-only logic; see the test below for the regression).
        var utcNow = new DateTime(2026, 9, 15, 18, 30, 0, DateTimeKind.Utc);

        ShopCalendar.Today(utcNow, Karachi).Should().Be(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    // Rule: "today" is the shop's LOCAL calendar date, expressed as UTC midnight.
    // Why:  this is the production bug from the audit: a karigar checking in at 11 PM Karachi
    //       time (18:30 UTC, still the same UTC calendar day, see the test above) was fine, but
    //       one checking in at 00:30 AM Karachi time (19:30 UTC the PREVIOUS day) got
    //       attributed to yesterday under the old UTC-only logic. This test locks in the fix.
    [Fact]
    public void Today_JustAfterMidnightInKarachi_ButStillPreviousUtcDate_RollsForwardToKarachiDate()
    {
        // 2026-09-16 00:30 PKT = 2026-09-15 19:30 UTC — UTC still says the 15th, but the shop's
        // wall clock has already turned over to the 16th. "Today" must be the 16th.
        var utcNow = new DateTime(2026, 9, 15, 19, 30, 0, DateTimeKind.Utc);

        ShopCalendar.Today(utcNow, Karachi).Should().Be(new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Today_ResultAlwaysHasUtcKind()
        => ShopCalendar.Today(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), Karachi).Kind.Should().Be(DateTimeKind.Utc);

    // ───────────────────────────────────────────────────────────────
    // LocalTimeOfDay — drives the check-in timestamp
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void LocalTimeOfDay_ConvertsUtcToKarachiWallClock()
    {
        // 09:15 UTC = 14:15 PKT
        var utcNow = new DateTime(2026, 9, 15, 9, 15, 0, DateTimeKind.Utc);

        ShopCalendar.LocalTimeOfDay(utcNow, Karachi).Should().Be(new TimeSpan(14, 15, 0));
    }

    [Fact]
    public void LocalTimeOfDay_TruncatesSecondsAndBelow()
        => ShopCalendar.LocalTimeOfDay(new DateTime(2026, 9, 15, 9, 15, 47, DateTimeKind.Utc), Karachi)
            .Should().Be(new TimeSpan(14, 15, 0), "check-in times are recorded to the minute, not the second");

    // ───────────────────────────────────────────────────────────────
    // ToCalendarDate — normalising a client-supplied date to the storage convention
    // ───────────────────────────────────────────────────────────────

    // Rule: a value already at UTC midnight is treated as a bare calendar date and kept as-is,
    //       WITHOUT being converted through the shop's time zone.
    // Why:  "2026-08-09" (no time zone in the request) deserialises to exactly this. Karachi is
    //       a positive offset, so converting midnight-UTC through it never actually crosses a
    //       date boundary — this test alone couldn't tell "kept as-is" from "converted anyway".
    //       America/New_York (a negative offset, no DST in use here as it's a fixed instant) is
    //       used specifically because converting 2026-08-09T00:00Z through it WOULD land on
    //       2026-08-08 — so this only passes if the midnight short-circuit genuinely skips the
    //       conversion the rule below performs.
    [Fact]
    public void ToCalendarDate_AlreadyUtcMidnight_IsKeptUnchanged_NotConvertedThroughTimeZone()
    {
        var newYork = ShopCalendar.ResolveTimeZone("America/New_York");
        var bareDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

        ShopCalendar.ToCalendarDate(bareDate, newYork).Should().Be(bareDate);
    }

    // Rule: an offset-bearing instant is converted to the shop's local date, not truncated in UTC.
    // Why:  this is the exact scenario from the audit — a client sends
    //       "2026-08-09T00:00:00+05:00" (midnight Karachi time), which arrives here as
    //       2026-08-08T19:00:00Z. Naively taking .Date on the UTC value would store 8 August
    //       instead of the 9th the customer actually meant.
    [Fact]
    public void ToCalendarDate_OffsetInstantAtLocalMidnight_ResolvesToTheIntendedLocalDate()
    {
        var offsetInstant = new DateTime(2026, 8, 8, 19, 0, 0, DateTimeKind.Utc); // = 2026-08-09T00:00+05:00

        ShopCalendar.ToCalendarDate(offsetInstant, Karachi).Should().Be(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));
    }

    // Rule: an Unspecified-kind value is read as if its clock numbers were already UTC, then
    //       converted to the shop's local date like any other instant.
    // Why:  a midnight input can't distinguish "treated as UTC" from "treated as already-local"
    //       (both give the same date). A non-midnight time can: 20:00 read as UTC becomes
    //       01:00 the next day in Karachi (+5h) — a different date than 20:00 read as
    //       already-local (which would stay on the same day).
    [Fact]
    public void ToCalendarDate_UnspecifiedKind_IsTreatedAsUtc_ThenConvertedToLocalDate()
    {
        var unspecified = DateTime.SpecifyKind(new DateTime(2026, 8, 9, 20, 0, 0), DateTimeKind.Unspecified);

        ShopCalendar.ToCalendarDate(unspecified, Karachi).Should().Be(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    // ───────────────────────────────────────────────────────────────
    // LocalDayBounds / LocalDateOf
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void LocalDayBounds_ReturnsHalfOpenUtcRangeCoveringTheLocalDay()
    {
        var calendarDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

        var (start, end) = ShopCalendar.LocalDayBounds(calendarDate, Karachi);

        start.Should().Be(new DateTime(2026, 9, 14, 19, 0, 0, DateTimeKind.Utc), "2026-09-15 00:00 PKT");
        end.Should().Be(new DateTime(2026, 9, 15, 19, 0, 0, DateTimeKind.Utc), "2026-09-16 00:00 PKT");
    }

    [Fact]
    public void LocalDateOf_RangeStart_MapsBackToTheCalendarDate()
    {
        var calendarDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var (start, _) = ShopCalendar.LocalDayBounds(calendarDate, Karachi);

        ShopCalendar.LocalDateOf(start, Karachi).Should().Be(calendarDate);
    }

    [Fact]
    public void LocalDateOf_OneMinuteBeforeRangeEnd_StillMapsToTheCalendarDate()
    {
        var calendarDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var (_, end) = ShopCalendar.LocalDayBounds(calendarDate, Karachi);

        ShopCalendar.LocalDateOf(end.AddMinutes(-1), Karachi).Should().Be(calendarDate);
    }

    // Rule: LocalDayBounds is a half-open range [start, end) — end itself belongs to the NEXT day.
    // Why:  this is what "half-open" actually means and what every caller (the delivery board,
    //       the reminder job) relies on: filtering with "< end" must not include a second's worth
    //       of the next calendar day. Without this test, end could map back to the same day and
    //       nothing here would catch it.
    [Fact]
    public void LocalDateOf_RangeEndItself_MapsToTheNextCalendarDate()
    {
        var calendarDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var (_, end) = ShopCalendar.LocalDayBounds(calendarDate, Karachi);

        ShopCalendar.LocalDateOf(end, Karachi).Should().Be(calendarDate.AddDays(1));
    }
}
