using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// Bounds <c>prerender_demand</c>: the pages no visitor was refused for
/// <see cref="DemandOptions.RetentionDays"/> go, whatever their state but rendering, then the
/// least requested pending pages past <see cref="DemandOptions.MaxPendingPages"/>.
/// </summary>
internal sealed class DemandPruner(
    IDbContextFactory<LoDbDbContext> contexts,
    IOptions<DemandOptions> options,
    TimeProvider clock)
{
    /// <summary>Removes the rows past their retention or the bound; how many went.</summary>
    public async Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var forgotten = clock.GetUtcNow() - TimeSpan.FromDays(settings.RetentionDays);
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
        return stale + surplus;
    }
}
