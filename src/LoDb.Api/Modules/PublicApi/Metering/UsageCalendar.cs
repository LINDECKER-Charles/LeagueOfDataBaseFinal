namespace LoDb.Api.Modules.PublicApi.Metering;

/// <summary>
/// The UTC day a request is counted on, and the month its quota belongs to, read from the
/// clock of the host.
/// </summary>
internal sealed class UsageCalendar(TimeProvider timeProvider)
{
    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>First day of the current UTC month.</summary>
    public DateOnly CurrentMonth => MonthOf(Today);

    public static DateOnly MonthOf(DateOnly day) => new(day.Year, day.Month, 1);
}
