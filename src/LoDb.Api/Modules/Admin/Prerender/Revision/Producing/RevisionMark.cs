using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>
/// The watermark of the producer, <c>prerender_revision_watermark</c>: the closures of one
/// generation of the index enqueued so far, and the coverage they were read from, read and
/// moved inside the producer's transaction.
/// </summary>
/// <remarks>
/// The row is locked for the transaction (<c>FOR UPDATE</c>): two producers, a promotion and a
/// reconciliation on two instances, take their turn, the second reading what the first moved.
/// Before the first run there is no row: it is written for generation 0, which no generation
/// of the index has, so that the first run takes the current one as new.
/// </remarks>
internal static class RevisionMark
{
    private const string Ensure = """
        INSERT INTO prerender_revision_watermark (id, generation_id, closed_through)
        VALUES (1, 0, @epoch)
        ON CONFLICT (id) DO NOTHING
        """;

    private const string Lock = """
        SELECT id FROM prerender_revision_watermark WHERE id = 1 FOR UPDATE
        """;

    /// <summary>
    /// The watermark, locked until the transaction of <paramref name="db"/> ends.
    /// </summary>
    public static async Task<PrerenderRevisionWatermark> LockAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.ExecuteSqlRawAsync(
            Ensure,
            [new NpgsqlParameter("epoch", DateTimeOffset.UnixEpoch)],
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(Lock, cancellationToken);
        return await db.PrerenderRevisionWatermarks.AsNoTracking()
            .SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Moves the watermark to the generation, the closure and the coverage of
    /// <paramref name="to"/>.
    /// </summary>
    public static Task MoveAsync(
        LoDbDbContext db,
        PrerenderRevisionWatermark to,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(to);
        return db.PrerenderRevisionWatermarks.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(mark => mark.GenerationId, to.GenerationId)
                .SetProperty(mark => mark.ClosedThrough, to.ClosedThrough)
                .SetProperty(mark => mark.CoveredThrough, to.CoveredThrough)
                .SetProperty(mark => mark.UpdatedAt, to.UpdatedAt),
            cancellationToken);
    }
}
