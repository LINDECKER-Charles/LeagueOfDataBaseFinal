using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// Writes the captured page views continuously: at the first view in the queue it waits
/// <c>LoDb:Analytics:BatchWindow</c>, so that the next ones join it, then writes all of them
/// in one copy per batch.
/// </summary>
/// <remarks>
/// Every instance runs it, for the views its own endpoints took in. On stop, the views still
/// queued are written within the host's shutdown timeout; beyond, they are lost.
/// </remarks>
internal sealed class AnalyticsWriterWorker(
    IPageViewPump pump,
    IOptions<AnalyticsOptions> options,
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
            // The host would not wait any longer: the views left are lost.
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
