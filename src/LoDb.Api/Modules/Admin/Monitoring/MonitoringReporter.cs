using System.Data.Common;
using LoDb.Api.Modules.Admin.Monitoring.Views;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Caching.Hybrid;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// Assembles the monitoring report, kept thirty seconds so that the page and the badges of
/// the overview share one round of probes; a refresh asks for a new one.
/// </summary>
/// <remarks>
/// Each part that reads the database fails on its own: a database down still shows the
/// probes, the queues of this instance and the figures of the process.
/// </remarks>
internal sealed partial class MonitoringReporter(
    HybridCache cache,
    ServiceProbes probes,
    DatabaseFigures figures,
    VersionOverview versions,
    ProcessSampler process,
    IVersionBacklog versionBacklog,
    IOnDemandBacklog onDemandBacklog,
    TimeProvider clock,
    ILogger<MonitoringReporter> logger)
{
    private const string CacheKey = "lodb:admin:monitoring";

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    // Figures of this instance: another instance keeps its own.
    private static readonly HybridCacheEntryOptions CacheEntry = new()
    {
        Expiration = Lifetime,
        LocalCacheExpiration = Lifetime,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<MonitoringReport> ReportAsync(bool refresh, CancellationToken cancellation)
    {
        if (refresh)
        {
            await cache.RemoveAsync(CacheKey, cancellation);
        }

        return await cache.GetOrCreateAsync(
            CacheKey,
            this,
            static (reporter, cancel) => reporter.AssembleAsync(cancel),
            CacheEntry,
            cancellationToken: cancellation);
    }

    private async ValueTask<MonitoringReport> AssembleAsync(CancellationToken cancellation)
    {
        var outbox = await TryAsync(figures.OutboxAsync, cancellation);
        return new MonitoringReport
        {
            GeneratedAt = clock.GetUtcNow(),
            Services = await probes.ProbeAsync(cancellation),
            Counters = await TryAsync(figures.CountersAsync, cancellation),
            Ingestion = new IngestionState
            {
                VersionBacklog = versionBacklog.Count,
                OnDemandBacklog = onDemandBacklog.Count,
                OutboxPending = outbox?.Pending,
                OutboxDead = outbox?.Dead,
            },
            Versions = await TryAsync(versions.ReadAsync, cancellation),
            Process = process.Sample(),
            Tables = await TryAsync(figures.TablesAsync, cancellation) ?? [],
        };
    }

    private async Task<T?> TryAsync<T>(
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellation)
    {
        try
        {
            return await read(cancellation);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            LogUnreadable(logger, exception);
            return default;
        }
    }

    [LoggerMessage(
        EventName = "admin.monitoring.database_unreadable",
        Level = LogLevel.Warning,
        Message = "A part of the monitoring report could not be read from the database.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception);
}
