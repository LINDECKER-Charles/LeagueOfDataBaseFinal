using LoDb.Api.Modules.Admin.Prerender.Writing;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Writes the running pass of the pre-rendering (ADR 0011), on one instance at a time: every
/// half minute it looks for one, then works through it for as long as it runs.
/// </summary>
/// <remarks>
/// <para>
/// The engine is resolved at each tick, from a scope of its own, and the tick does nothing when
/// it is not registered or not configured: the worker sleeps until the module and its settings
/// are there. The advisory lock of the engine keeps a second instance, or the command line,
/// from writing the same pass.
/// </para>
/// <para>
/// A failed tick (the database out of reach, the catalog's versions unreadable) is logged once
/// and tried again at the next one; the pass resumes at its cursor.
/// </para>
/// </remarks>
internal sealed partial class PrerenderWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<PrerenderWorker> logger) : BackgroundService
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
            if (scope.ServiceProvider.GetService<PrerenderEngine>() is { } engine)
            {
                await engine.RunAsync(stoppingToken);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogTickFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "prerender.tick.failed",
        Level = LogLevel.Error,
        Message = "The pre-rendering pass stopped on an error; it resumes at the next tick.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
