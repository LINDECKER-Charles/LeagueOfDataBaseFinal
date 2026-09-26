using LoDb.Infrastructure.Analytics.Aggregation;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// Compares two daily aggregates field by field. The order of a counter map is left out:
/// PostgreSQL's jsonb does not keep it, and no report depends on it.
/// </summary>
internal static class AggregateAssert
{
    public static void Equal(DailyAggregate expected, DailyAggregate actual)
    {
        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(expected.Views, actual.Views);
        Assert.Equal(expected.BotViews, actual.BotViews);
        Assert.Equal(expected.ByHour, actual.ByHour);
        Assert.Equal(expected.ByWeekday, actual.ByWeekday);
        Assert.Equal(expected.Visitors, actual.Visitors);
        Assert.Equal(Sorted(expected.CountryNames), Sorted(actual.CountryNames));
        foreach (var name in DailyBuckets.Names)
        {
            Assert.True(
                Sorted(expected.Buckets[name]).SequenceEqual(Sorted(actual.Buckets[name])),
                $"The {name} counters differ.");
        }
    }

    private static List<KeyValuePair<string, T>> Sorted<T>(IReadOnlyDictionary<string, T> map) =>
        [.. map.OrderBy(static entry => entry.Key, StringComparer.Ordinal)];
}
