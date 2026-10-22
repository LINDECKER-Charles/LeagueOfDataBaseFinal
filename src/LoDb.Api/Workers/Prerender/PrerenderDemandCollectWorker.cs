using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Brings the pages the visitors' SSR refused into the automatic queue of the pre-rendering,
/// every half minute (<see cref="DemandCollector"/>).
/// </summary>
/// <remarks>
/// The collector is resolved at each tick and the tick does nothing without it, nor while the
/// queue is off. No lock: a read made by two instances adds nothing twice. A failed tick is
/// logged and the next one reads again from the same cursor.
/// </remarks>
internal sealed partial class PrerenderDemandCollectWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<PrerenderDemandCollectWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, clock);
        do
        {
            await TickAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            if (scope.ServiceProvider.GetService<DemandCollector>() is { } collector)
            {
                await collector.CollectAsync(stoppingToken);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogTickFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "prerender.demand.collect_failed",
        Level = LogLevel.Error,
        Message = "The automatic queue could not take in the SSR's refused pages; it reads them"
            + " again at the next tick.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
