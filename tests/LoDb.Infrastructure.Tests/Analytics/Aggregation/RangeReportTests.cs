using System.Text.Json;
using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Tests.Analytics.Support;

namespace LoDb.Infrastructure.Tests.Analytics.Aggregation;

/// <summary>
/// The report of the sample's days equals the legacy <c>RangeReportBuilder</c>'s, field by
/// field: totals, series, rankings and their shares, hours, weekdays and heatmap.
/// </summary>
public sealed class RangeReportTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReportEqualsTheLegacyOne()
    {
        var report = RangeReportBuilder.Build(await LegacyDailiesAsync(), AnalyticsRange.All);

        using var expected = LegacySamples.Json("report.json");
        JsonAssert.Equivalent(
            expected.RootElement,
            JsonSerializer.SerializeToElement(report, Web));
    }

    [Fact]
    public async Task VisitorSeenOnTwoDaysIsReturning()
    {
        var dailies = await LegacyDailiesAsync();
        var first = dailies[0].Visitors.ToHashSet(StringComparer.Ordinal);

        var report = RangeReportBuilder.Build(dailies, AnalyticsRange.All);

        Assert.Equal(
            dailies.SelectMany(static daily => daily.Visitors).Distinct().Count(),
            report.Totals.UniqueVisitors);
        Assert.True(report.Totals.ReturningVisitors <= report.Totals.UniqueVisitors);
        Assert.Contains(dailies[1].Visitors, first.Contains);
    }

    [Fact]
    public void EmptyPeriodGivesAnEmptyReport()
    {
        var day = new DateOnly(2026, 9, 26);

        var report = RangeReportBuilder.Build([new DailyAggregate(day)], AnalyticsRange.LastWeek);

        Assert.Equal(0, report.Totals.Views);
        Assert.Empty(report.TopPages);
        Assert.Equal(AnalyticsRange.LastWeek, report.Range);
        Assert.Equal(day, Assert.Single(report.Series).Date);
    }

    private static async Task<List<DailyAggregate>> LegacyDailiesAsync()
    {
        var dailies = new List<DailyAggregate>();
        foreach (var file in LegacyDailyFile.In(LegacySamples.DailyFolder))
        {
            dailies.Add((await LegacyDailyFile.ReadAsync(file, Cancellation))!);
        }

        return dailies;
    }
}
