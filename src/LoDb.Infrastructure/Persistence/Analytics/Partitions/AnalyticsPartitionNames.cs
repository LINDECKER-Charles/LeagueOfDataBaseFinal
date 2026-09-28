using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoDb.Infrastructure.Persistence.Analytics.Partitions;

/// <summary>Names and bounds of the daily partitions of <c>analytics_event</c>.</summary>
public static class AnalyticsPartitionNames
{
    public const string Parent = "analytics_event";

    private const string Prefix = Parent + "_";
    private const string DayFormat = "yyyyMMdd";

    /// <summary>The partition of a day, such as <c>analytics_event_20260926</c>.</summary>
    public static string Of(DateOnly day) =>
        Prefix + day.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>The day of a partition name; false for any other table.</summary>
    public static bool TryParseDay(string name, [NotNullWhen(true)] out DateOnly? day)
    {
        ArgumentNullException.ThrowIfNull(name);
        day = name.StartsWith(Prefix, StringComparison.Ordinal)
            && name.Length == Prefix.Length + DayFormat.Length
            && DateOnly.TryParseExact(
                name[Prefix.Length..],
                DayFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : null;
        return day is not null;
    }

    /// <summary>First instant of the UTC day: the lower bound of its partition.</summary>
    public static DateTimeOffset StartOf(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
