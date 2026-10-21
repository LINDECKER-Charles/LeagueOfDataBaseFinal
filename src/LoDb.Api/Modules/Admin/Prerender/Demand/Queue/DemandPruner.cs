using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// Bounds <c>prerender_demand</c>: the pages no visitor was refused for
/// <see cref="DemandOptions.RetentionDays"/> go, whatever their state but rendering, then the
/// least requested pending pages past <see cref="DemandOptions.MaxPendingPages"/>. A page
/// failed for a day is tried again: still refused to visitors, it would never be pruned.
/// </summary>
internal sealed class DemandPruner(
    IDbContextFactory<LoDbDbContext> contexts,
    IOptions<DemandOptions> options,
    TimeProvider clock)
{
    /// <summary>How long a failed page rests before the queue tries it again.</summary>
    public static readonly TimeSpan FailedRest = TimeSpan.FromDays(1);

    /// <summary>
    /// Removes the rows past their retention or the bound, and puts the pages failed for a day
    /// back to pending; how many rows moved.
    /// </summary>
    public async Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var forgotten = now - TimeSpan.FromDays(settings.RetentionDays);
        var rested = now - FailedRest;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var stale = await db.PrerenderDemand
            .Where(page => page.State != PrerenderDemandState.Rendering
                && page.LastSeenAt < forgotten)
            .ExecuteDeleteAsync(cancellationToken);
        var surplus = await db.PrerenderDemand
            .Where(static page => page.State == PrerenderDemandState.Pending)
            .OrderByDescending(static page => page.Demand)
            .ThenBy(static page => page.FirstSeenAt)
            .Skip(settings.MaxPendingPages)
            .ExecuteDeleteAsync(cancellationToken);
        var retried = await db.PrerenderDemand
            .Where(page => page.State == PrerenderDemandState.Failed
                && page.UpdatedAt < rested)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, PrerenderDemandState.Pending)
                    .SetProperty(page => page.Attempts, 0)
                    .SetProperty(page => page.Epoch, (int?)null),
                cancellationToken);
        return stale + surplus + retried;
    }
}
