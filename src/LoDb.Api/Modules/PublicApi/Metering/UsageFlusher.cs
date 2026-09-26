using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Metering;

/// <summary>
/// Runs the <see cref="UsageMeter"/>: its consumer, a flush every
/// <c>LoDb:PublicApi:MeteringInterval</c>, and a last one when the host stops.
/// </summary>
/// <remarks>
/// <para>
/// Registered by the module rather than under <c>Workers</c>: without it the billed
/// requests would never reach <c>api_usage</c>, so it runs wherever <c>/v1</c> is served,
/// with the workers switched off too. The server stops before it does, so the last flush
/// holds every request served.
/// </para>
/// <para>
/// A host may be stopped twice at once, as under <c>WebApplicationFactory</c> where the
/// application stops it too: every stop waits for the one last flush, so that the host is
/// never disposed under it.
/// </para>
/// </remarks>
internal sealed class UsageFlusher(
    UsageMeter meter,
    IOptions<PublicApiOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly Lock _gate = new();
    private Task? _stopped;

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _stopped ??= StopThenFlushAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var collecting = meter.CollectAsync(stoppingToken);
        using var timer = new PeriodicTimer(options.Value.MeteringInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await meter.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: StopAsync writes what is left.
        }

        try
        {
            await collecting;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Same.
        }
    }

    private async Task StopThenFlushAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await meter.FlushAsync(CancellationToken.None);
    }
}
