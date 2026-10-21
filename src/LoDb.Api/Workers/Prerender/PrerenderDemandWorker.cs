using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Renders the automatic queue of the pre-rendering on one instance at a time: every half
/// minute it looks for pending pages, then writes them, the most requested first, until none
/// is left (<see cref="DemandDrain"/>).
/// </summary>
/// <remarks>
/// The drain is resolved at each tick and the tick does nothing without it, nor while the queue
/// is off or the store not configured. A failed tick is logged once and tried again at the
/// next one; a page it left rendering goes back to pending at the next start.
/// </remarks>
internal sealed partial class PrerenderDemandWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<PrerenderDemandWorker> logger) : BackgroundService
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
            if (scope.ServiceProvider.GetService<DemandDrain>() is { } drain)
            {
                await drain.RunAsync(stoppingToken);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogTickFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "prerender.demand.tick_failed",
        Level = LogLevel.Error,
        Message = "The automatic queue stopped on an error; it resumes at the next tick.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
