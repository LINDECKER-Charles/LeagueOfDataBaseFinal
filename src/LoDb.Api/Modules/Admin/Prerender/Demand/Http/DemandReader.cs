using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// Reads the automatic queue for the admin: three small queries, the counts by state, the
/// pages written lately (on their own index) and the ten most requested pending pages (on the
/// worker's).
/// </summary>
internal sealed class DemandReader(
    IDbContextFactory<LoDbDbContext> contexts,
    DemandActivation activation,
    IOptions<DemandOptions> demand,
    IOptions<PrerenderOptions> pass,
    TimeProvider clock)
{
    /// <summary>The pending pages the admin lists.</summary>
    public const int TopCount = 10;

    public async Task<PrerenderDemandReport> ReadAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var states = await db.PrerenderDemand.AsNoTracking()
            .GroupBy(static page => page.State)
            .Select(static group => new { State = group.Key, Pages = group.LongCount() })
            .ToDictionaryAsync(
                static row => row.State,
                static row => row.Pages,
                cancellationToken);
        return new PrerenderDemandReport
        {
            GeneratedAt = now,
            IsEnabled = activation.IsEnabled,
            IsActive = activation.IsActive,
            IsCapturing = activation.RefusedPagesUrl is not null,
            QueueSharePercent = demand.Value.QueueSharePercent,
            Epoch = pass.Value.ServedEpoch,
            Pending = states.GetValueOrDefault(PrerenderDemandState.Pending),
            Done = states.GetValueOrDefault(PrerenderDemandState.Done),
            Failed = states.GetValueOrDefault(PrerenderDemandState.Failed),
            Skipped = states.GetValueOrDefault(PrerenderDemandState.Skipped),
            WrittenLastHour = await WrittenSinceAsync(db, now.AddHours(-1), cancellationToken),
            WrittenLastDay = await WrittenSinceAsync(db, now.AddDays(-1), cancellationToken),
            Top = await TopAsync(db, cancellationToken),
        };
    }

    private static Task<long> WrittenSinceAsync(
        LoDbDbContext db,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        db.PrerenderDemand.LongCountAsync(page => page.RenderedAt >= since, cancellationToken);

    private static async Task<IReadOnlyList<PrerenderDemandPage>> TopAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken) =>
        await db.PrerenderDemand.AsNoTracking()
            .Where(static page => page.State == PrerenderDemandState.Pending)
            .OrderByDescending(static page => page.Demand)
            .ThenBy(static page => page.FirstSeenAt)
            .Take(TopCount)
            .Select(static page => new PrerenderDemandPage
            {
                Path = page.Path,
                Version = page.Version,
                Locale = page.Locale,
                Demand = page.Demand,
                Attempts = page.Attempts,
                LastSeenAt = page.LastSeenAt,
                Reason = page.Reason,
            })
            .ToListAsync(cancellationToken);
}
