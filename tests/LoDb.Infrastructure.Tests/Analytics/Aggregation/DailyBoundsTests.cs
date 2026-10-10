using System.Globalization;
using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Tests.Persistence.Analytics;

namespace LoDb.Infrastructure.Tests.Analytics.Aggregation;

/// <summary>
/// A day folded from its views stays within its bounds whatever its clients send: the
/// referring hosts beyond the bound are counted under one key, the visitors beyond theirs are
/// left out and the day flagged, every view still counted; an aggregate read back is not cut.
/// </summary>
public sealed class DailyBoundsTests
{
    private const int MaxRefererHosts = 200;
    private const string Search = "www.google.com";

    private static readonly DateTimeOffset At = new(2026, 9, 26, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = DateOnly.FromDateTime(At.UtcDateTime);

    [Fact]
    public void HostsBeyondTheBoundAreCountedUnderTheOthers()
    {
        var daily = Bounded(maxVisitors: int.MaxValue);
        FoldReferredBy(daily, Enumerable.Repeat(Search, 50));

        FoldReferredBy(daily, Enumerable.Range(0, 10_000).Select(Host));

        var hosts = daily.Buckets[DailyBuckets.RefHost];
        Assert.InRange(hosts.Count, 2, MaxRefererHosts + 1);
        Assert.Equal(50, hosts[Search]);
        Assert.True(hosts[DailyBuckets.OtherRefererHosts] > 0);
        Assert.Equal(10_050, hosts.Values.Sum());
        Assert.Equal(10_050, daily.Views);
    }

    [Fact]
    public void HostsWithinTheBoundAreCountedExactly()
    {
        var daily = Bounded(maxVisitors: int.MaxValue);

        FoldReferredBy(daily, Enumerable.Range(0, MaxRefererHosts).Select(Host).Append(Host(0)));

        var hosts = daily.Buckets[DailyBuckets.RefHost];
        Assert.Equal(MaxRefererHosts, hosts.Count);
        Assert.Equal(2, hosts[Host(0)]);
        Assert.DoesNotContain(DailyBuckets.OtherRefererHosts, hosts.Keys);
    }

    [Fact]
    public void VisitorsBeyondTheBoundAreLeftOutAndTheDayFlagged()
    {
        var daily = Bounded(maxVisitors: 3);

        foreach (var visitor in new[] { "a", "b", "a", "c", "d", "e", "b" })
        {
            var view = AnalyticsSamples.View(At);
            view.Visitor = visitor;
            DailyFold.Fold(daily, view);
        }

        Assert.Equal(["a", "b", "c"], daily.Visitors);
        Assert.True(daily.VisitorsTruncated);
        Assert.Equal(7, daily.Views);
    }

    [Fact]
    public void FlagSurvivesTheJsonColumnsAndOnlyATruncatedDayHasIt()
    {
        var truncated = new DailyAggregate(Day) { Views = 1, VisitorsTruncated = true };
        var whole = new DailyAggregate(Day) { Views = 1 };

        Assert.True(DailyColumns.Read(RowOf(truncated)).VisitorsTruncated);
        Assert.False(DailyColumns.Read(RowOf(whole)).VisitorsTruncated);
        Assert.DoesNotContain(
            DailyColumns.VisitorsTruncatedField,
            DailyColumns.TotalsOf(whole),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateReadBackKeepsEveryHost()
    {
        var stored = new DailyAggregate(Day);
        foreach (var host in Enumerable.Range(0, MaxRefererHosts * 2).Select(Host))
        {
            stored.Count(DailyBuckets.RefHost, host, 1);
        }

        var read = DailyColumns.Read(RowOf(stored));

        Assert.Equal(MaxRefererHosts * 2, read.Buckets[DailyBuckets.RefHost].Count);
    }

    private static DailyAggregate Bounded(int maxVisitors) => new(
        Day,
        new DailyLimits { MaxVisitors = maxVisitors, MaxRefererHosts = MaxRefererHosts });

    private static string Host(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"h{index}.example");

    private static void FoldReferredBy(DailyAggregate daily, IEnumerable<string> hosts)
    {
        foreach (var host in hosts)
        {
            var view = AnalyticsSamples.View(At);
            view.RefererHost = host;
            DailyFold.Fold(daily, view);
        }
    }

    private static DailyRow RowOf(DailyAggregate daily) => new()
    {
        Day = daily.Day,
        Source = AnalyticsDailySource.Events,
        Totals = DailyColumns.TotalsOf(daily),
        Buckets = DailyColumns.BucketsOf(daily),
        Visitors = DailyColumns.VisitorsOf(daily),
        CountryNames = DailyColumns.CountryNamesOf(daily),
    };
}
