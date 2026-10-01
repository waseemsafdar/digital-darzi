namespace Application.Common;

/// <summary>
/// Pure calendar arithmetic for shop-local dates.
///
/// Convention used across the API: a <em>calendar date</em> (delivery date, expense date,
/// attendance date, trial date) is stored as UTC midnight of the shop-local date. So a shop in
/// Asia/Karachi whose delivery is on 9 August stores <c>2026-08-09T00:00:00Z</c>, and "due today"
/// compares against the shop-local date expressed the same way.
/// </summary>
public static class ShopCalendar
{
    public const string FallbackTimeZoneId = "Asia/Karachi";

    /// <summary>Resolves an IANA (or Windows) id; falls back to Asia/Karachi, then UTC.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        foreach (var candidate in new[] { id, FallbackTimeZoneId, "Pakistan Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static bool IsKnownTimeZone(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { return false; }
    }

    /// <summary>The shop-local date for <paramref name="utcNow"/>, as UTC midnight.</summary>
    public static DateTime Today(DateTime utcNow, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcNow), tz);
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    /// <summary>Shop-local wall-clock time of day for <paramref name="utcNow"/>, to the minute.</summary>
    public static TimeSpan LocalTimeOfDay(DateTime utcNow, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcNow), tz);
        return new TimeSpan(local.Hour, local.Minute, 0);
    }

    /// <summary>
    /// Normalises a client-supplied date to the storage convention.
    /// A value that is exactly midnight UTC is treated as a bare calendar date (that is what
    /// "2026-08-09" deserialises to) and kept. Any other instant, e.g. "2026-08-09T00:00:00+05:00"
    /// which arrives as 2026-08-08T19:00Z, is converted to the shop's local date first.
    /// </summary>
    public static DateTime ToCalendarDate(DateTime value, TimeZoneInfo tz)
    {
        var utc = AsUtc(value);
        if (utc.TimeOfDay == TimeSpan.Zero)
            return DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    /// <summary>UTC instants bounding a shop-local calendar day: [start, end).</summary>
    public static (DateTime StartUtc, DateTime EndUtc) LocalDayBounds(DateTime calendarDate, TimeZoneInfo tz)
    {
        var localMidnight = DateTime.SpecifyKind(calendarDate.Date, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        var end = TimeZoneInfo.ConvertTimeToUtc(localMidnight.AddDays(1), tz);
        return (start, end);
    }

    /// <summary>The shop-local calendar date on which a UTC instant falls (UTC-midnight form).</summary>
    public static DateTime LocalDateOf(DateTime instant, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(instant), tz);
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
