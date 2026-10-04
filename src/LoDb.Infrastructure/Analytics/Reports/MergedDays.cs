using LoDb.Infrastructure.Analytics.Aggregation;

namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// The days of a period merged as the legacy <c>RangeReportBuilder::mergeAllMaps</c> does:
/// counts summed, the first name of a country kept.
/// </summary>
internal sealed class MergedDays
{
    private readonly Dictionary<string, Dictionary<string, long>> _maps;
    private readonly Dictionary<string, string> _countryNames = new(StringComparer.Ordinal);

    public MergedDays(IReadOnlyList<DailyAggregate> dailies)
    {
        ArgumentNullException.ThrowIfNull(dailies);
        ArgumentOutOfRangeException.ThrowIfZero(dailies.Count);
        Dailies = dailies;
        _maps = DailyBuckets.Names.ToDictionary(
            static name => name,
            static _ => new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var daily in dailies)
        {
            Add(daily);
        }
    }

    /// <summary>The days, in order.</summary>
    public IReadOnlyList<DailyAggregate> Dailies { get; }

    public IReadOnlyDictionary<string, string> CountryNames => _countryNames;

    /// <summary>A counter map of <see cref="DailyBuckets.Names"/>, summed over the days.</summary>
    public IReadOnlyDictionary<string, long> this[string bucket] => _maps[bucket];

    private void Add(DailyAggregate daily)
    {
        foreach (var (name, counts) in daily.Buckets)
        {
            var merged = _maps[name];
            foreach (var (key, count) in counts)
            {
                merged[key] = merged.GetValueOrDefault(key) + count;
            }
        }

        foreach (var (code, name) in daily.CountryNames)
        {
            _countryNames.TryAdd(code, name);
        }
    }
}
