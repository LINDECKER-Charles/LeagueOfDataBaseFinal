namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// The aggregate of one UTC day, the mergeable unit of the reports: what a row of
/// <c>analytics_daily</c> and a legacy file <c>analytics/daily/{date}.json</c> hold.
/// </summary>
internal sealed class DailyAggregate
{
    private readonly List<string> _visitors = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public DailyAggregate(DateOnly day)
    {
        Day = day;
        Buckets = DailyBuckets.Names.ToDictionary(
            static name => name,
            static _ => new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public DateOnly Day { get; }

    /// <summary>Views of people; robots are only counted in <see cref="BotViews"/>.</summary>
    public long Views { get; set; }

    public long BotViews { get; set; }

    /// <summary>Distinct visitor hashes of the day, in the order they were first seen.</summary>
    public IReadOnlyList<string> Visitors => _visitors;

    /// <summary>Views by UTC hour, 0 to 23.</summary>
    public long[] ByHour { get; } = new long[DailyBuckets.HoursPerDay];

    /// <summary>Views by UTC weekday, Monday first.</summary>
    public long[] ByWeekday { get; } = new long[DailyBuckets.DaysPerWeek];

    /// <summary>Name of each country code counted.</summary>
    public Dictionary<string, string> CountryNames { get; } = new(StringComparer.Ordinal);

    /// <summary>The maps of <see cref="DailyBuckets.Names"/>, each key to its count.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, long>> Buckets { get; }

    /// <summary>Whether the day saw no view at all, not even a robot's.</summary>
    public bool IsEmpty => Views == 0 && BotViews == 0;

    public void AddVisitor(string visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        if (_seen.Add(visitor))
        {
            _visitors.Add(visitor);
        }
    }

    public void Count(string bucket, string key, long by)
    {
        var counts = Buckets[bucket];
        counts[key] = counts.GetValueOrDefault(key) + by;
    }
}
