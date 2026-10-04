using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// The period of a report, as the legacy admin names it: the last 7, 30 or 90 days, today
/// included, or all the history kept.
/// </summary>
public sealed record AnalyticsRange
{
    /// <summary>The period when none is asked for.</summary>
    public const string Default = "30d";

    public const string LastWeek = "7d";
    public const string LastQuarter = "90d";
    public const string All = "all";

    private const int WeekDays = 7;
    private const int MonthDays = 30;
    private const int QuarterDays = 90;

    private static readonly FrozenDictionary<string, int?> Spans =
        new Dictionary<string, int?>(StringComparer.Ordinal)
        {
            [LastWeek] = WeekDays,
            [Default] = MonthDays,
            [LastQuarter] = QuarterDays,
            [All] = null,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private AnalyticsRange(string name, int? days)
    {
        Name = name;
        Days = days;
    }

    /// <summary>Every period name: <c>7d</c>, <c>30d</c>, <c>90d</c>, <c>all</c>.</summary>
    public static IReadOnlyList<string> Names { get; } = [LastWeek, Default, LastQuarter, All];

    public string Name { get; }

    /// <summary>Days of the period; null for all the history.</summary>
    public int? Days { get; }

    /// <summary>Reads a period name exactly as <see cref="Names"/> spells it.</summary>
    public static bool TryParse(string? name, [NotNullWhen(true)] out AnalyticsRange? range)
    {
        range = name is not null && Spans.TryGetValue(name, out var days)
            ? new AnalyticsRange(name, days)
            : null;
        return range is not null;
    }
}
