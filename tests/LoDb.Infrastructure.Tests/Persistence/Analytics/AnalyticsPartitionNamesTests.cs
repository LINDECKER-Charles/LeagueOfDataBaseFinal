using LoDb.Infrastructure.Persistence.Analytics.Partitions;

namespace LoDb.Infrastructure.Tests.Persistence.Analytics;

/// <summary>
/// A partition's name gives back its day; any other table, however close its name, gives
/// none.
/// </summary>
public sealed class AnalyticsPartitionNamesTests
{
    // A partition, then one row per check that refuses: prefix, length, calendar.
    [Theory]
    [InlineData("analytics_event_20260926", true)]
    [InlineData("analytics_daily", false)]
    [InlineData("analytics_event_archive", false)]
    [InlineData("analytics_event_20260230", false)]
    public void PartitionNamesRoundTrip(string name, bool isPartition)
    {
        Assert.Equal(isPartition, AnalyticsPartitionNames.TryParseDay(name, out var day));
        if (isPartition)
        {
            Assert.Equal(name, AnalyticsPartitionNames.Of(day!.Value));
        }
    }
}
