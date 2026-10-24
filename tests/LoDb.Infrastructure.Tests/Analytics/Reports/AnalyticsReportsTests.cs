using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Analytics.Reports.Breakdowns;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Reports;

/// <summary>
/// The admin's report from <c>analytics_daily</c>: the legacy days imported give the legacy
/// report, the days without an aggregate count as empty, today is folded on the spot.
/// </summary>
public sealed class AnalyticsReportsTests(PostgresContainerFixture postgres)
    : AnalyticsDatabase(postgres)
{
    // The legacy report had no previous totals: a report of all the history has none either.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private IAnalyticsReports Reports => Get<IAnalyticsReports>();

    // The first day of the legacy sample: each test moves on from there.
    protected override DateTimeOffset Start => new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportedDaysGiveTheLegacyReport()
    {
        await ImportLegacyDaysAsync();
        Time.SetUtcNow(new DateTimeOffset(2026, 7, 16, 18, 0, 0, TimeSpan.Zero));
        await WriteViewsAsync(At(17));

        var report = await Reports.BuildAsync(Range(AnalyticsRange.All), Cancellation);

        // No GeoLite2 database is provisioned for the tests.
        JsonAssert.Equivalent(
            LegacySamples.Report(geoAvailable: false),
            JsonSerializer.SerializeToElement(report, Web));
    }

    [Fact]
    public async Task DaysWithoutAggregateCountAsEmptyAndTodayIsLive()
    {
        await ImportLegacyDaysAsync();
        Time.SetUtcNow(new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero));
        await WriteViewsAsync(At(9), At(10));

        var report = await Reports.BuildAsync(Range(AnalyticsRange.LastWeek), Cancellation);

        Assert.Equal((new DateOnly(2026, 7, 12), Today, 7), (report.From, report.To, report.Days));
        Assert.Equal(
            [0L, 0L, 16L, 17L, 20L, 0L, 2L],
            report.Series.Select(static day => day.Views));
        Assert.Equal(55, report.Totals.Views);
    }

    [Fact]
    public async Task AllStartsAtTheFirstAggregate()
    {
        await ImportLegacyDaysAsync();
        Time.SetUtcNow(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));

        var report = await Reports.BuildAsync(Range(AnalyticsRange.All), Cancellation);

        Assert.Equal((new DateOnly(2026, 7, 14), 7), (report.From, report.Days));
    }

    [Fact]
    public async Task APeriodComparesWithThePeriodBeforeIt()
    {
        await ImportLegacyDaysAsync();
        Time.SetUtcNow(new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero));

        var week = await Reports.BuildAsync(Range(AnalyticsRange.LastWeek), Cancellation);
        var all = await Reports.BuildAsync(Range(AnalyticsRange.All), Cancellation);

        // 2026-07-16 (20 views) opens the week; 07-14 and 07-15 (16 and 17) close the one before.
        Assert.Equal((20L, 33L), (week.Totals.Views, week.Previous?.Views));
        Assert.Null(all.Previous);
    }

    [Fact]
    public async Task ABreakdownListsEveryKeyOfThePeriod()
    {
        await ImportLegacyDaysAsync();
        Time.SetUtcNow(new DateTimeOffset(2026, 7, 16, 18, 0, 0, TimeSpan.Zero));
        var whole = await Reports.BuildAsync(Range(AnalyticsRange.All), Cancellation);

        var pages = await Reports.BreakdownAsync(
            Range(AnalyticsRange.All),
            new AnalyticsBreakdownQuery { Dimension = AnalyticsDimensions.Pages },
            Cancellation);

        Assert.Equal(whole.Totals.PagesTracked, pages.Matched);
        Assert.Equal(whole.TopPages[0], pages.Rows[0]);
    }

    [Fact]
    public async Task EmptyHistoryReportsTodayAlone()
    {
        var report = await Reports.BuildAsync(Range(AnalyticsRange.All), Cancellation);

        Assert.Equal((Today, Today, 1), (report.From, report.To, report.Days));
        Assert.Equal(0, report.Totals.Views);
    }

    private static AnalyticsRange Range(string name) =>
        AnalyticsRange.TryParse(name, out var range) ? range : throw new ArgumentException(name);

    private async Task ImportLegacyDaysAsync()
    {
        var import = Get<ILegacyDailyImport>();
        var report = await import.ImportAsync(LegacySamples.DailyFolder, false, Cancellation);
        Assert.Equal(3, report.Imported);
    }
}
