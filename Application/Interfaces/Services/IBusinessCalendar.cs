namespace Application.Interfaces.Services;

/// <summary>
/// Shop-aware clock for the current request's branch. Wraps <see cref="Common.ShopCalendar"/>
/// with the branch's configured time zone and an injectable <see cref="TimeProvider"/>.
/// </summary>
public interface IBusinessCalendar
{
    DateTime UtcNow { get; }
    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct = default);

    /// <summary>Shop-local today, as UTC midnight (the calendar-date storage convention).</summary>
    Task<DateTime> TodayAsync(CancellationToken ct = default);

    /// <summary>Shop-local wall-clock time, to the minute.</summary>
    Task<TimeSpan> LocalTimeOfDayAsync(CancellationToken ct = default);

    /// <summary>Normalises a client-supplied calendar date to the storage convention.</summary>
    Task<DateTime> ToCalendarDateAsync(DateTime value, CancellationToken ct = default);
}
