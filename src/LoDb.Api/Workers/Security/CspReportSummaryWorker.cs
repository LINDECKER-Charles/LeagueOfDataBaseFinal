using LoDb.Api.Hosting.SecurityReports;

namespace LoDb.Api.Workers.Security;

/// <summary>
/// Writes the summary of the Content-Security-Policy reports once a minute
/// (<see cref="CspReportSummary"/>), only for a minute in which some came in.
/// </summary>
/// <remarks>
/// No advisory lock, unlike the periodic jobs (ADR 0003): each instance summarizes the reports
/// its own endpoint took in.
/// </remarks>
internal sealed class CspReportSummaryWorker(
    CspReportTally tally,
    TimeProvider timeProvider,
    ILogger<CspReportSummaryWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            CspReportSummary.Write(logger, tally.Drain(), Interval);
        }
    }
}
