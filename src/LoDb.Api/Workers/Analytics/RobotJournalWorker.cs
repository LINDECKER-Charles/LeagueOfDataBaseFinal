using LoDb.Infrastructure.Robots;
using LoDb.Infrastructure.Robots.Capture;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// Writes the robots journal continuously: at the first record in the queue it waits
/// <c>LoDb:Robots:BatchWindow</c>, so that the next ones join it, then turns the batch into
/// hits and writes them.
/// </summary>
/// <remarks>
/// Every instance runs it, for the records its own endpoint took in. On stop, the records
/// still queued are written within the host's shutdown timeout; beyond, they are lost.
/// </remarks>
internal sealed class RobotJournalWorker(
    IRobotJournalPump pump,
    IOptions<RobotsOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await pump.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host would not wait any longer: the records left are lost.
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var window = options.Value.BatchWindow;
        while (await pump.WaitAsync(stoppingToken))
        {
            await Task.Delay(window, timeProvider, stoppingToken);
            await pump.FlushAsync(stoppingToken);
        }
    }
}
