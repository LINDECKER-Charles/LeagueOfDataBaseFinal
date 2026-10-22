using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>
/// Fills the queue of the closed states (<c>prerender_revision</c>): the pages of every state the
/// index closed since its watermark, each version but those the last promotion made older
/// patches, in the 21 locales, for the served epoch and for the epoch the active pass writes,
/// due once the servers read an index that covers the closure.
/// </summary>
/// <remarks>
/// <para>
/// Called by the promotion, after the index extended (<c>PromotionStage.Prerender</c>), and by
/// the reconciliation of the pre-rendering, which heals a promotion whose notice was lost:
/// idempotent through the watermark, on any instance, whatever <c>AutoEnqueueOnPromotion</c>
/// says. It runs whenever the store is configured, the staging included.
/// </para>
/// <para>
/// A generation the watermark does not name (the first run, a rebuild of the index) is
/// history: its closures are what the passes render, never enqueued, but for those after the
/// version the last promotion replaced, which that promotion made on the generation itself.
/// A copy of the index older than the watermark's generation (a rebuild another process read
/// first) moves nothing.
/// </para>
/// </remarks>
internal sealed class RevisionProducer(
    PrerenderActivation activation,
    IEntityStateIndex index,
    PromotedVersions promoted,
    IPrerenderCatalog catalog,
    IDbContextFactory<LoDbDbContext> contexts,
    IOptions<PrerenderOptions> options,
    RevisionMetrics metrics,
    TimeProvider clock,
    ILogger<RevisionProducer> logger)
{
    private const string EpochSeparator = ", ";

    /// <summary>
    /// Enqueues what the closures since the watermark changed; the rows written.
    /// </summary>
    /// <param name="promotedVersion">
    /// The version a promotion just made the latest, whose closures the index must cover first;
    /// null for a reconciliation.
    /// </param>
    /// <param name="cancellationToken">Cancels the reads and the writes.</param>
    public async Task<int> ProduceAsync(
        PatchVersion? promotedVersion,
        CancellationToken cancellationToken)
    {
        if (!activation.IsActive)
        {
            return 0;
        }

        var snapshot = await index.GetAsync(cancellationToken);
        if (snapshot.Generation is null)
        {
            return 0;
        }

        if (promotedVersion is not null && !snapshot.Coverage.Contains(promotedVersion))
        {
            RevisionLog.IndexBehind(logger, promotedVersion.Value);
            return 0;
        }

        var leaving = await promoted.PreviousOfLatestAsync(cancellationToken);
        return await EnqueueAsync(snapshot, leaving, cancellationToken);
    }

    private async Task<int> EnqueueAsync(
        EntityStateSnapshot snapshot,
        PatchVersion? leaving,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var mark = await RevisionMark.LockAsync(db, cancellationToken);
        if (mark.GenerationId > snapshot.Generation!.Id)
        {
            return 0;
        }

        var since = SinceOf(mark, snapshot, leaving);
        var closed = snapshot.States.Where(state => state.ClosedAt > since).ToList();
        var enqueue = new RevisionEnqueue
        {
            Pages = ClosedStatePages.Of(closed, snapshot.Coverage, leaving),
            Epochs = await EpochsAsync(db, cancellationToken),
            Now = clock.GetUtcNow(),
            DueAt = clock.GetUtcNow() + catalog.SettlingDelay,
        };
        var rows = await RevisionRows.EnqueueAsync(db, enqueue, cancellationToken);
        var through = closed.Select(static state => state.ClosedAt).Append(since).Max()!.Value;
        await RevisionMark.MoveAsync(db, MarkOf(snapshot, through), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Report(new Produced(enqueue, closed.Count, since, snapshot.Generation!.Id, rows));
        return rows;
    }

    // Where the closures to enqueue start: the watermark of its generation; for another one,
    // what that generation closed before the last promotion, or when it was built.
    private DateTimeOffset SinceOf(
        PrerenderRevisionWatermark mark,
        EntityStateSnapshot snapshot,
        PatchVersion? leaving)
    {
        var generation = snapshot.Generation!;
        if (mark.GenerationId == generation.Id)
        {
            return mark.ClosedThrough;
        }

        var history = snapshot.States
            .Where(state => state.Last is { } last && (leaving is null || last < leaving))
            .Select(static state => state.ClosedAt)
            .Append(generation.BuiltAt)
            .Max()!.Value;
        RevisionLog.NewGeneration(logger, generation.Id, history);
        return history;
    }

    // The served epoch, and the one the active pass writes when it is another.
    private async Task<IReadOnlyList<int>> EpochsAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var served = options.Value.ServedEpoch;
        var written = await db.PrerenderPasses.AsNoTracking()
            .Where(static pass => pass.IsActive)
            .Select(static pass => (int?)pass.Epoch)
            .SingleOrDefaultAsync(cancellationToken);
        return written is { } epoch && epoch != served ? [served, epoch] : [served];
    }

    private PrerenderRevisionWatermark MarkOf(
        EntityStateSnapshot snapshot,
        DateTimeOffset through) => new()
    {
        GenerationId = snapshot.Generation!.Id,
        ClosedThrough = through,
        UpdatedAt = clock.GetUtcNow(),
    };

    private void Report(Produced run)
    {
        metrics.Enqueued(run.Rows);
        if (run.Rows == 0)
        {
            return;
        }

        var epochs = string.Join(
            EpochSeparator,
            run.Enqueue.Epochs.Select(static epoch =>
                epoch.ToString(CultureInfo.InvariantCulture)));
        RevisionLog.Enqueued(
            logger,
            run.Rows,
            run.Enqueue.Pages.Count,
            run.States,
            run.Since,
            run.GenerationId,
            epochs,
            run.Enqueue.DueAt);
    }

    // What one run wrote, for its line and its metric.
    private sealed record Produced(
        RevisionEnqueue Enqueue,
        int States,
        DateTimeOffset Since,
        long GenerationId,
        int Rows);
}
