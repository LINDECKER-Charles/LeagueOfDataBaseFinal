using LoDb.Api.Hosting.SecurityReports;
using LoDb.Api.Workers.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Hosting.SecurityReports;

/// <summary>
/// One line a minute for the reports of that minute, whatever their number, and none for a
/// minute without any.
/// </summary>
public sealed class CspReportSummaryWorkerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WritesOneLineAMinuteForTheReportsOfThatMinute()
    {
        var tally = new CspReportTally();
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<CspReportSummaryWorker>();
        using var worker = new CspReportSummaryWorker(tally, time, logger);
        await worker.StartAsync(Token);

        for (var report = 0; report < 50; report++)
        {
            tally.Record(CspReportRead.Of([new("script-src-elem", "enforce")]));
        }

        tally.Record(CspReportRead.Of([new("trusted-types", "report")]));
        tally.Record(CspReportRead.Refused(CspReportRefusal.Oversized));
        tally.RecordShed();
        await TickUntilLoggedAsync(time, logger);
        time.Advance(CspReportSummaryWorker.Interval);
        await worker.StopAsync(Token);

        var line = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal("web.csp.violated", line.Id.Name);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal(
            "51 policy violations reported in 60 s: script-src-elem/enforce=50,"
                + " trusted-types/report=1; bodies refused: Oversized=1; shed: 1.",
            line.Message);
    }

    [Fact]
    public async Task WritesNothingForAMinuteWithoutReports()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<CspReportSummaryWorker>();
        using var worker = new CspReportSummaryWorker(new CspReportTally(), time, logger);
        await worker.StartAsync(Token);

        for (var minute = 0; minute < 3; minute++)
        {
            time.Advance(CspReportSummaryWorker.Interval);
            await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
        }

        await worker.StopAsync(Token);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    // The worker starts its timer on a thread of its own: the minutes pass until it has ticked.
    private static async Task TickUntilLoggedAsync(FakeTimeProvider time, FakeLogger logger)
    {
        while (logger.Collector.Count == 0)
        {
            time.Advance(CspReportSummaryWorker.Interval);
            await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
        }
    }
}
