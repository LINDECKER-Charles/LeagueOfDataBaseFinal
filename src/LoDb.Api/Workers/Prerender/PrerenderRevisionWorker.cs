using LoDb.Api.Modules.Admin.Prerender.Revision.Drain;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Renders the queue of the pages of the closed states on one instance at a time: every half
/// minute it looks for pages that are due, then writes them until none is left
/// (<see cref="RevisionDrain"/>).
/// </summary>
/// <remarks>
/// The drain is resolved at each tick and the tick does nothing without it, nor while the store
/// is not configured. A failed tick is logged once and tried again at the next one; a page it
/// left rendering goes back to pending then.
/// </remarks>
internal sealed partial class PrerenderRevisionWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<PrerenderRevisionWorker> logger) : BackgroundService
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
            if (scope.ServiceProvider.GetService<RevisionDrain>() is { } drain)
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
        EventName = "prerender.revision.tick_failed",
        Level = LogLevel.Error,
        Message = "The queue of the closed states stopped on an error; it resumes at the next"
            + " tick.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
