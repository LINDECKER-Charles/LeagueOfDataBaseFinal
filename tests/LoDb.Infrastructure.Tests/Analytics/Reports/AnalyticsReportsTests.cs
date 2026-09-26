using System.Text.Json;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Analytics.Reports;
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
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

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

        using var expected = LegacySamples.Json("report.json");
        JsonAssert.Equivalent(expected.RootElement, JsonSerializer.SerializeToElement(report, Web));
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
