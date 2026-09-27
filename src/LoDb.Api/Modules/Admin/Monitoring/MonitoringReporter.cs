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
/// probes, the queues of this instance and the figures of the process. Such a report is
/// answered but not kept. The requests that overlap share one assembly, which reads through
/// a scope of its own: the request that started it may end or be aborted first.
/// </remarks>
internal sealed partial class MonitoringReporter(
    HybridCache cache,
    IServiceScopeFactory scopes,
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

        var attempt = new Attempt();
        var report = await cache.GetOrCreateAsync(
            CacheKey,
            (Reporter: this, Attempt: attempt),
            static (state, cancel) => state.Reporter.AssembleAsync(state.Attempt, cancel),
            CacheEntry,
            cancellationToken: cancellation);
        if (attempt.Degraded)
        {
            // Only the caller whose assembly ran knows it failed; the next one reads again.
            await cache.RemoveAsync(CacheKey, cancellation);
        }

        return report;
    }

    private async ValueTask<MonitoringReport> AssembleAsync(
        Attempt attempt,
        CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var figures = services.GetRequiredService<DatabaseFigures>();
        var versions = services.GetRequiredService<VersionOverview>();
        var outbox = await TryAsync(attempt, figures.OutboxAsync, cancellation);
        return new MonitoringReport
        {
            GeneratedAt = clock.GetUtcNow(),
            Services = await services.GetRequiredService<ServiceProbes>().ProbeAsync(cancellation),
            Counters = await TryAsync(attempt, figures.CountersAsync, cancellation),
            Ingestion = new IngestionState
            {
                VersionBacklog = versionBacklog.Count,
                OnDemandBacklog = onDemandBacklog.Count,
                OutboxPending = outbox?.Pending,
                OutboxDead = outbox?.Dead,
            },
            Versions = await TryAsync(attempt, versions.ReadAsync, cancellation),
            Process = process.Sample(),
            Tables = await TryAsync(attempt, figures.TablesAsync, cancellation) ?? [],
        };
    }

    private async Task<T?> TryAsync<T>(
        Attempt attempt,
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
            attempt.Degraded = true;
            return default;
        }
    }

    [LoggerMessage(
        EventName = "admin.monitoring.database_unreadable",
        Level = LogLevel.Warning,
        Message = "A part of the monitoring report could not be read from the database.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception);

    /// <summary>Whether the assembly run for this caller missed a part of the database.</summary>
    private sealed class Attempt
    {
        public bool Degraded { get; set; }
    }
}
