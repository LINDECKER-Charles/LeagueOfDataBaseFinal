namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// The aggregate of one UTC day, the mergeable unit of the reports: what a row of
/// <c>analytics_daily</c> and a legacy file <c>analytics/daily/{date}.json</c> hold.
/// </summary>
/// <remarks>
/// Folded from the views, a day is bounded by its <see cref="DailyLimits"/>: a client sets
/// its own visitor hash and Referer, so that a single one could otherwise grow the day, its
/// memory and its row without end.
/// </remarks>
internal sealed class DailyAggregate
{
    private readonly List<string> _visitors = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly DailyLimits _limits;

    /// <summary>An unbounded day: one read back from its row or imported.</summary>
    public DailyAggregate(DateOnly day)
        : this(day, DailyLimits.None)
    {
    }

    public DailyAggregate(DateOnly day, DailyLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Day = day;
        _limits = limits;
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

    /// <summary>
    /// Whether visitors were left out past <see cref="DailyLimits.MaxVisitors"/>: the day's
    /// count of visitors, and the unique visitors of a period that holds it, are then floors.
    /// </summary>
    public bool VisitorsTruncated { get; set; }

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
        if (_seen.Contains(visitor))
        {
            return;
        }

        if (_seen.Count >= _limits.MaxVisitors)
        {
            VisitorsTruncated = true;
            return;
        }

        _seen.Add(visitor);
        _visitors.Add(visitor);
    }

    public void Count(string bucket, string key, long by)
    {
        var counts = Buckets[bucket];
        if (bucket == DailyBuckets.RefHost && IsBeyondTrackedHosts(counts, key))
        {
            FoldLeastCountedHosts(counts);
        }

        counts[key] = counts.GetValueOrDefault(key) + by;
    }

    private static int TrackedHosts(Dictionary<string, long> counts) =>
        counts.Count - (counts.ContainsKey(DailyBuckets.OtherRefererHosts) ? 1 : 0);

    // The least counted half of the hosts makes room, their views kept under the others' key.
    // A host that refers often keeps its count; a stream of hosts seen once each costs one
    // sort per half of the bound, and the map never outgrows the bound.
    private static void FoldLeastCountedHosts(Dictionary<string, long> counts)
    {
        var least = counts
            .Where(static entry => entry.Key != DailyBuckets.OtherRefererHosts)
            .OrderBy(static entry => entry.Value)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, TrackedHosts(counts) / 2))
            .ToList();
        foreach (var (host, count) in least)
        {
            counts.Remove(host);
            counts[DailyBuckets.OtherRefererHosts] =
                counts.GetValueOrDefault(DailyBuckets.OtherRefererHosts) + count;
        }
    }

    private bool IsBeyondTrackedHosts(Dictionary<string, long> counts, string host) =>
        host != DailyBuckets.OtherRefererHosts
        && !counts.ContainsKey(host)
        && TrackedHosts(counts) >= _limits.MaxRefererHosts;
}
