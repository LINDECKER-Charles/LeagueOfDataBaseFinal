using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Catalog;

/// <summary>
/// Pins the catalogs of the latest version in this instance's memory at startup, then again
/// each time the latest version changes (<see cref="ICatalogPinning"/>).
/// </summary>
/// <remarks>
/// It looks every <c>LoDb:Catalog:VersionsLifetime</c>, the delay within which a promotion
/// shows anyway, one second at least; a pass with nothing new costs one cached read. No
/// advisory lock, unlike the periodic jobs (ADR 0003): what it fills is the memory of the
/// instance it runs in, which every instance must fill for itself.
/// </remarks>
internal sealed partial class LatestCatalogWarmer(
    ICatalogPinning pinning,
    IOptions<CatalogOptions> options,
    TimeProvider timeProvider,
    ILogger<LatestCatalogWarmer> logger) : BackgroundService
{
    private static readonly TimeSpan MinimumPeriod = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = options.Value.VersionsLifetime < MinimumPeriod
            ? MinimumPeriod
            : options.Value.VersionsLifetime;
        using var timer = new PeriodicTimer(period, timeProvider);
        do
        {
            await PinAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // A failed pass, such as the database out of reach, is retried at the next tick.
    private async Task PinAsync(CancellationToken stoppingToken)
    {
        try
        {
            await pinning.PinLatestAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogPinningFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "catalog.pinning.failed",
        Level = LogLevel.Warning,
        Message = "The catalogs of the latest version could not be pinned; next try soon.")]
    private static partial void LogPinningFailed(ILogger logger, Exception exception);
}
