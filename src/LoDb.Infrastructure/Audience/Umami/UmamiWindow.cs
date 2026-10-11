namespace LoDb.Infrastructure.Audience.Umami;

/// <summary>
/// A span of time as Umami's statistics take it: both ends included, in Unix milliseconds.
/// </summary>
internal sealed record UmamiWindow(DateTimeOffset Start, DateTimeOffset End)
{
    private static readonly TimeSpan LastInstant = TimeSpan.FromMilliseconds(1);

    public long StartAt => Start.ToUnixTimeMilliseconds();

    public long EndAt => End.ToUnixTimeMilliseconds();

    /// <summary>The whole UTC day.</summary>
    public static UmamiWindow Of(DateOnly day)
    {
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return new(start, start.AddDays(1) - LastInstant);
    }

    /// <summary>The last hour that ended before <paramref name="now"/>.</summary>
    public static UmamiWindow HourBefore(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var end = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
        return new(end.AddHours(-1), end - LastInstant);
    }
}
