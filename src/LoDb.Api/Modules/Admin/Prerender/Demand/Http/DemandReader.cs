using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// Reads the automatic queue for the admin, in small queries: the counts by state, the pages
/// written lately (on their own index), what it holds in the served epoch (on the index of the
/// pages stored), its last 24 hours, and the ten most requested pending pages (on the
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
        var epoch = pass.Value.ServedEpoch;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var states = await StatesAsync(db, cancellationToken);
        var served = await ServedAsync(db, epoch, cancellationToken);
        return new PrerenderDemandReport
        {
            GeneratedAt = now,
            IsEnabled = activation.IsEnabled,
            IsActive = activation.IsActive,
            IsCapturing = activation.RefusedPagesUrl is not null,
            QueueSharePercent = demand.Value.QueueSharePercent,
            Epoch = epoch,
            Pending = states.GetValueOrDefault(PrerenderDemandState.Pending),
            Rendering = states.GetValueOrDefault(PrerenderDemandState.Rendering),
            Done = states.GetValueOrDefault(PrerenderDemandState.Done),
            Failed = states.GetValueOrDefault(PrerenderDemandState.Failed),
            Skipped = states.GetValueOrDefault(PrerenderDemandState.Skipped),
            WrittenLastHour = await WrittenSinceAsync(db, now.AddHours(-1), cancellationToken),
            WrittenLastDay = await WrittenSinceAsync(db, now.AddDays(-1), cancellationToken),
            ServedEpochPages = served.Kept?.Pages ?? 0,
            ServedEpochBytes = served.Kept?.Bytes ?? 0,
            Superseded = served.Rewritten?.Pages ?? 0,
            Hours = await DemandHours.ReadAsync(db, now, cancellationToken),
            Top = await TopAsync(db, cancellationToken),
        };
    }

    private static Task<Dictionary<PrerenderDemandState, long>> StatesAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken) =>
        db.PrerenderDemand.AsNoTracking()
            .GroupBy(static page => page.State)
            .Select(static group => new { State = group.Key, Pages = group.LongCount() })
            .ToDictionaryAsync(
                static row => row.State,
                static row => row.Pages,
                cancellationToken);

    // The pages the queue wrote into the served epoch, apart as a pass wrote them again or not.
    private static async Task<ServedPages> ServedAsync(
        LoDbDbContext db,
        int epoch,
        CancellationToken cancellationToken)
    {
        var rows = await db.PrerenderDemand.AsNoTracking()
            .Where(page => page.State == PrerenderDemandState.Done && page.Epoch == epoch)
            .GroupBy(static page => page.SupersededAt == null)
            .Select(static group => new StoredFigures(
                group.Key,
                group.LongCount(),
                group.Sum(static page => page.Bytes ?? 0)))
            .ToListAsync(cancellationToken);
        return new ServedPages(
            rows.SingleOrDefault(static row => row.IsKept),
            rows.SingleOrDefault(static row => !row.IsKept));
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

    // Done rows of the served epoch, kept as the queue's (no pass wrote them again) or not.
    private sealed record StoredFigures(bool IsKept, long Pages, long Bytes);

    private sealed record ServedPages(StoredFigures? Kept, StoredFigures? Rewritten);
}
