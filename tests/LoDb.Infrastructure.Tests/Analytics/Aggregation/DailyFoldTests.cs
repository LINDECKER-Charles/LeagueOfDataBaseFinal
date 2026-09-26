using System.Globalization;
using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Infrastructure.Tests.Persistence.Analytics;

namespace LoDb.Infrastructure.Tests.Analytics.Aggregation;

/// <summary>
/// A day folded from its views equals the day file the legacy <c>AnalyticsAggregator</c>
/// wrote from the same views, and survives the JSON columns of <c>analytics_daily</c>.
/// </summary>
public sealed class DailyFoldTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 26, 8, 30, 0, TimeSpan.Zero);

    public static TheoryData<string> Days => ["2026-07-14", "2026-07-15", "2026-07-16"];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Days))]
    public async Task FoldEqualsTheLegacyDayFile(string day)
    {
        var folded = Fold(DateOnly.Parse(day, CultureInfo.InvariantCulture));

        var expected = await LegacyDailyFile.ReadAsync(
            Path.Combine(LegacySamples.DailyFolder, day + ".json"),
            Cancellation);

        Assert.NotNull(expected);
        Assert.True(folded.Views > 0);
        AggregateAssert.Equal(expected, folded);
    }

    [Fact]
    public void EntityIsCountedUnderItsTypeAndLegacyKey()
    {
        var daily = new DailyAggregate(DateOnly.FromDateTime(At.UtcDateTime));
        var entities = new (string Type, string? Key)[]
        {
            ("champion", "Ahri"), ("item", "1004"), ("runesReforged", "Domination"),
            ("summoner", "SummonerFlash"), ("home", null),
        };
        foreach (var (type, key) in entities)
        {
            var view = AnalyticsSamples.View(At);
            view.Type = type;
            view.Entity = key;
            DailyFold.Fold(daily, view);
        }

        Assert.Equal(
            ["champion:Ahri", "item:1004", "runesReforged:Domination", "summoner:SummonerFlash"],
            daily.Buckets[DailyBuckets.Entities].Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RobotViewIsCountedApart()
    {
        var daily = new DailyAggregate(DateOnly.FromDateTime(At.UtcDateTime));
        var view = AnalyticsSamples.View(At);
        view.IsBot = true;

        DailyFold.Fold(daily, view);

        Assert.Equal(0, daily.Views);
        Assert.Equal(1, daily.BotViews);
        Assert.Empty(daily.Buckets[DailyBuckets.Pages]);
    }

    [Fact]
    public void AggregateSurvivesTheJsonColumns()
    {
        var folded = Fold(new DateOnly(2026, 7, 15));
        var row = new DailyRow
        {
            Day = folded.Day,
            Source = AnalyticsDailySource.Events,
            Totals = DailyColumns.TotalsOf(folded),
            Buckets = DailyColumns.BucketsOf(folded),
            Visitors = DailyColumns.VisitorsOf(folded),
            CountryNames = DailyColumns.CountryNamesOf(folded),
        };

        AggregateAssert.Equal(folded, DailyColumns.Read(row));
    }

    private static DailyAggregate Fold(DateOnly day)
    {
        var daily = new DailyAggregate(day);
        foreach (var view in LegacySamples.Events()
            .Where(view => DateOnly.FromDateTime(view.OccurredAt.UtcDateTime) == day))
        {
            DailyFold.Fold(daily, view);
        }

        return daily;
    }
}
