using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using LoDb.Ingestion.Catalog.States.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>
/// Fills the queue of the closed states (<c>prerender_revision</c>): the pages of every state the
/// index closed since its watermark, each of its versions, in the 21 locales, for the served
/// epoch, the epoch the active pass writes and every newer one a completed pass filled (an
/// epoch on standby for its switch), due once the servers read an index that covers the
/// closure.
/// </summary>
/// <remarks>
/// <para>
/// Called by the promotion, after the index extended (<c>PromotionStage.Prerender</c>), and by
/// the reconciliation of the pre-rendering, which heals a promotion whose notice was lost, each
/// with an index that covers the latest patch (<see cref="CoveringIndex"/>): idempotent through
/// the watermark, on any instance, whatever <c>AutoEnqueueOnPromotion</c> says. It runs
/// whenever the store is configured, the staging included.
/// </para>
/// <para>
/// A generation the watermark does not name (the first run, a rebuild of the index) is
/// history: its closures are what the passes render, never enqueued, but for those after the
/// version the last promotion replaced, which that promotion made on the generation itself, or
/// which a rebuild that walked past an index left behind the promotions dated after its build.
/// A copy of the index older than the watermark's generation (a rebuild another process read
/// first) moves nothing.
/// </para>
/// </remarks>
internal sealed class RevisionProducer(
    PrerenderActivation activation,
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
    /// <param name="snapshot">The index, covering the latest patch.</param>
    /// <param name="cancellationToken">Cancels the reads and the writes.</param>
    public async Task<int> ProduceAsync(
        EntityStateSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!activation.IsActive || snapshot.Generation is null)
        {
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

        var due = DueOf(mark, snapshot, leaving);
        var closed = snapshot.States.Where(due.Includes).ToList();
        var enqueue = new RevisionEnqueue
        {
            Pages = ClosedStatePages.Of(closed, snapshot.Coverage),
            Epochs = await EpochsAsync(db, cancellationToken),
            Now = clock.GetUtcNow(),
            DueAt = clock.GetUtcNow() + catalog.SettlingDelay,
        };
        var rows = await RevisionRows.EnqueueAsync(db, enqueue, cancellationToken);
        var through = closed.Select(static state => state.ClosedAt).Append(due.Since).Max()!.Value;
        await RevisionMark.MoveAsync(db, MarkOf(snapshot, through), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Report(new Produced(enqueue, closed.Count, due.Since, snapshot.Generation!.Id, rows));
        return rows;
    }

    // The closures to enqueue: past the watermark of its generation; for another one, past
    // what that generation closed before the last promotion, or when it was built, and those
    // of the last promotion dated after the build.
    private DueClosures DueOf(
        PrerenderRevisionWatermark mark,
        EntityStateSnapshot snapshot,
        PatchVersion? leaving)
    {
        var generation = snapshot.Generation!;
        if (mark.GenerationId == generation.Id)
        {
            return new DueClosures(mark.ClosedThrough, null);
        }

        var history = snapshot.States
            .Where(state => state.Last is { } last && (leaving is null || last < leaving))
            .Select(static state => state.ClosedAt)
            .Append(generation.BuiltAt)
            .Max()!.Value;
        RevisionLog.NewGeneration(logger, generation.Id, history);
        var lastPromotion = leaving is null ? null : new LastPromotion(leaving, generation.BuiltAt);
        return new DueClosures(history, lastPromotion);
    }

    // The served epoch, the one the active pass writes, and every newer one a completed pass
    // filled: on standby for its switch, it must not keep the pages of the states closed since.
    private async Task<IReadOnlyList<int>> EpochsAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var served = options.Value.ServedEpoch;
        var written = await db.PrerenderPasses.AsNoTracking()
            .Where(pass => pass.IsActive
                || (pass.State == PrerenderPassState.Completed && pass.Epoch > served))
            .Select(static pass => pass.Epoch)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [served, .. written.Where(epoch => epoch != served).Order()];
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

    // The closures past Since, and those of the last promotion closed after the build of a
    // generation the watermark does not name: a rebuild that walked past an index left behind
    // the promotions dates them alike with the earlier ones it walked.
    private sealed record DueClosures(DateTimeOffset Since, LastPromotion? LastPromotion)
    {
        public bool Includes(IndexedState state) =>
            state.ClosedAt > Since
            || (LastPromotion is { } promotion && state.Last is { } last
                && last >= promotion.Leaving && state.ClosedAt > promotion.BuiltAt);
    }

    // The version the last promotion replaced, and when the generation was built.
    private sealed record LastPromotion(PatchVersion Leaving, DateTimeOffset BuiltAt);

    // What one run wrote, for its line and its metric.
    private sealed record Produced(
        RevisionEnqueue Enqueue,
        int States,
        DateTimeOffset Since,
        long GenerationId,
        int Rows);
}
