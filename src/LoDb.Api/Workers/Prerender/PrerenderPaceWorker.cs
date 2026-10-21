using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Ticks the regulator of the pre-rendering pace every 10 s (<see cref="PaceSampler"/>): it
/// samples the host and the API's cgroup, and decides every half minute while a pass drains
/// on this instance.
/// </summary>
/// <remarks>
/// The sampler is resolved at each tick and the tick does nothing without it, nor while the
/// pass is not configured or its pace is fixed. A failed tick is logged once and the next one
/// samples again; meanwhile the allowance goes stale and falls back to the floor.
/// </remarks>
internal sealed class PrerenderPaceWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<PrerenderPaceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PaceTiming.SamplePeriod, clock);
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
            if (scope.ServiceProvider.GetService<PaceSampler>() is { } sampler)
            {
                await sampler.TickAsync(stoppingToken);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            PaceLog.TickFailed(logger, exception);
        }
    }
}
