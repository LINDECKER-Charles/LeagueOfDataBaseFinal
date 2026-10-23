using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// Notes that a pass wrote again a page the queue had written in the same epoch: the store
/// holds it once, under the pass's counters from then on, so the queue's share of the epoch
/// leaves it out (<c>prerender_demand.superseded_at</c>).
/// </summary>
/// <remarks>
/// One statement on the table's key per page the pass writes; a page the queue never wrote,
/// or wrote in another epoch, changes nothing.
/// </remarks>
internal sealed class DemandRewrites(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    /// <summary>
    /// Marks the page at <paramref name="path"/> (<c>/fr/16.19.1/items/1036-long-sword</c>)
    /// rewritten by a pass of <paramref name="epoch"/>, if the queue holds it there.
    /// </summary>
    public async Task MarkAsync(int epoch, string path, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        await using var db = await contexts.CreateDbContextAsync(token);
        await db.PrerenderDemand
            .Where(page => page.Path == path
                && page.State == PrerenderDemandState.Done
                && page.Epoch == epoch
                && page.SupersededAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(page => page.SupersededAt, now),
                token);
    }
}
