using LoDb.Infrastructure.Analytics.Aggregation;

namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// The report of a period from its daily aggregates, ported from the legacy
/// <c>RangeReportBuilder</c>: visitor sets are united, so that the unique visitors of a
/// period are exact; a visitor seen on two days or more is returning.
/// </summary>
/// <remarks>
/// A breakdown's ties are sorted by name: the order PHP kept, the order of first sight, is
/// not one a JSON column preserves.
/// </remarks>
internal static class RangeReportBuilder
{
    private const int TopPages = 20;
    private const int TopEntities = 20;
    private const int TopReferers = 15;
    private const int ReturningDays = 2;
    private const double Percent = 100;

    /// <param name="dailies">Every day of the period, in order; at least one.</param>
    /// <param name="range">Name of the period.</param>
    /// <param name="geoAvailable">Whether this instance resolves countries.</param>
    public static AnalyticsReport Build(
        IReadOnlyList<DailyAggregate> dailies,
        string range,
        bool geoAvailable) =>
        Build(new MergedDays(dailies), range, geoAvailable);

    private static AnalyticsReport Build(MergedDays days, string range, bool geoAvailable) => new()
    {
        Range = range,
        From = days.Dailies[0].Day,
        To = days.Dailies[^1].Day,
        Days = days.Dailies.Count,
        Totals = TotalsOf(days),
        Series = [.. days.Dailies.Select(SeriesDay)],
        ByType = Rank(days[DailyBuckets.ByType]),
        ByKind = Rank(days[DailyBuckets.ByKind]),
        ByRoute = Rank(days[DailyBuckets.ByRoute]),
        Status = Rank(days[DailyBuckets.Status]),
        TopPages = Rank(days[DailyBuckets.Pages], TopPages),
        TopEntities = Rank(days[DailyBuckets.Entities], TopEntities),
        ByHour = Sum(days.Dailies, static daily => daily.ByHour, DailyBuckets.HoursPerDay),
        ByWeekday = Sum(days.Dailies, static daily => daily.ByWeekday, DailyBuckets.DaysPerWeek),
        Heatmap = HeatmapOf(days[DailyBuckets.Heatmap]),
        Locale = Rank(days[DailyBuckets.Locale]),
        Lang = Rank(days[DailyBuckets.Lang]),
        Browser = Rank(days[DailyBuckets.Browser]),
        Os = Rank(days[DailyBuckets.Os]),
        Device = Rank(days[DailyBuckets.Device]),
        RefSource = Rank(days[DailyBuckets.RefSource]),
        TopReferers = Rank(days[DailyBuckets.RefHost], TopReferers),
        Country = Countries(days[DailyBuckets.Country], days.CountryNames),
        GeoAvailable = geoAvailable,
    };

    private static AnalyticsTotals TotalsOf(MergedDays days)
    {
        var daysSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var visitor in days.Dailies.SelectMany(static daily => daily.Visitors))
        {
            daysSeen[visitor] = daysSeen.GetValueOrDefault(visitor) + 1;
        }

        return new AnalyticsTotals
        {
            Views = days.Dailies.Sum(static daily => daily.Views),
            BotViews = days.Dailies.Sum(static daily => daily.BotViews),
            UniqueVisitors = daysSeen.Count,
            ReturningVisitors = daysSeen.Values.Count(static seen => seen >= ReturningDays),
            PagesTracked = days[DailyBuckets.Pages].Count,
        };
    }

    private static AnalyticsDay SeriesDay(DailyAggregate daily) => new()
    {
        Date = daily.Day,
        Views = daily.Views,
        Visitors = daily.Visitors.Count,
        BotViews = daily.BotViews,
    };

    private static long[] Sum(
        IReadOnlyList<DailyAggregate> dailies,
        Func<DailyAggregate, long[]> vector,
        int size)
    {
        var sums = new long[size];
        foreach (var values in dailies.Select(vector))
        {
            for (var i = 0; i < Math.Min(size, values.Length); i++)
            {
                sums[i] += values[i];
            }
        }

        return sums;
    }

    private static long[][] HeatmapOf(IReadOnlyDictionary<string, long> cells)
    {
        var grid = new long[DailyBuckets.DaysPerWeek][];
        for (var weekday = 0; weekday < grid.Length; weekday++)
        {
            grid[weekday] = new long[DailyBuckets.HoursPerDay];
        }

        foreach (var (cell, count) in cells)
        {
            if (DailyBuckets.TryParseCell(cell, out var weekday, out var hour))
            {
                grid[weekday][hour] = count;
            }
        }

        return grid;
    }

    private static List<AnalyticsCountry> Countries(
        IReadOnlyDictionary<string, long> codes,
        IReadOnlyDictionary<string, string> names)
    {
        double total = codes.Values.Sum();
        return
        [
            .. Sorted(codes).Select(entry => new AnalyticsCountry
            {
                Name = names.GetValueOrDefault(entry.Key, entry.Key),
                Code = entry.Key,
                Count = entry.Value,
                Pct = Share(entry.Value, total),
            }),
        ];
    }

    private static List<AnalyticsRank> Rank(
        IReadOnlyDictionary<string, long> counts,
        int limit = int.MaxValue)
    {
        double total = counts.Values.Sum();
        return
        [
            .. Sorted(counts).Take(limit).Select(entry => new AnalyticsRank
            {
                Name = entry.Key,
                Count = entry.Value,
                Pct = Share(entry.Value, total),
            }),
        ];
    }

    private static IEnumerable<KeyValuePair<string, long>> Sorted(
        IReadOnlyDictionary<string, long> counts) =>
        counts.OrderByDescending(static entry => entry.Value)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal);

    private static double Share(long count, double total) =>
        total > 0 ? count / total * Percent : 0;
}
