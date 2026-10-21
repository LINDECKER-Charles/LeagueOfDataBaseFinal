using System.Collections.Concurrent;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// The coverage of each epoch the report shows, folded again only when a unit of the epoch
/// ended since: the report is read every minute during a pass, and folding every unit of every
/// pass of each epoch each time would read tens of thousands of rows for nothing.
/// </summary>
/// <remarks>
/// The fold is stamped with the number of the epoch's units that ended and the latest end, an
/// aggregate over the partial index <c>ix_prerender_unit_ended</c> alone. A unit that starts
/// turns an absent cell into a partial one, two gaps alike: the summary does not depend on it.
/// A unit that ended before <c>ended_at</c> was kept never moves the stamp, and never changes.
/// </remarks>
internal sealed class CoverageCache(
    IDbContextFactory<LoDbDbContext> contexts,
    EpochCoverage coverage)
{
    private readonly ConcurrentDictionary<int, Entry> _entries = new();

    /// <summary>
    /// The cells of <paramref name="epoch"/>, folded again if one of its units ended since.
    /// </summary>
    public async Task<CoverageMap> ReadAsync(int epoch, CancellationToken cancellationToken)
    {
        var stamp = await StampAsync(epoch, cancellationToken);
        if (_entries.TryGetValue(epoch, out var entry) && entry.Stamp == stamp)
        {
            return entry.Map;
        }

        // Read after the stamp: a unit that ends in between folds in now, and again next time.
        var map = await coverage.ReadAsync(epoch, cancellationToken);
        _entries[epoch] = new Entry(stamp, map);
        return map;
    }

    private async Task<Stamp> StampAsync(int epoch, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var ended = db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.Pass!.Epoch == epoch && unit.EndedAt != null);
        return new Stamp(
            await ended.CountAsync(cancellationToken),
            await ended.MaxAsync(static unit => unit.EndedAt, cancellationToken));
    }

    private sealed record Stamp(int Ended, DateTimeOffset? LastEndedAt);

    private sealed record Entry(Stamp Stamp, CoverageMap Map);
}
