using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Building;
using LoDb.Ingestion.Catalog.States.Loading;
using LoDb.Ingestion.Catalog.States.Storage;
using LoDb.Ingestion.Catalog.States.Telemetry;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>
/// Catches the index of states up to the latest promoted version under its lock: no-op,
/// forward extension of the current generation, or a new generation made current.
/// </summary>
/// <remarks>
/// A promotion and the reconciliation first ingest the en_US datasets that the listed versions
/// after the coverage end lack: a version the promotion skipped is covered all the same. The
/// build command downloads nothing unless asked. Every update ends with one summary line and
/// invalidates the copy this process holds, whether it wrote or found the lock taken.
/// </remarks>
internal sealed class StateIndexUpdater(
    StateIndexLock indexLock,
    CoverageSource coverage,
    GenerationStore store,
    GenerationWalker walker,
    StateIndexCopy copy,
    StateIndexMetrics metrics,
    TimeProvider timeProvider,
    ILogger<StateIndexUpdater> logger) : IStateIndexUpdater
{
    // The command runs before the API serves, and fails the deploy when it gives up: it may
    // wait for an extension the serving image is finishing. The others leave it to the holder.
    private static readonly TimeSpan BuildLockWait = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);

    // The finest step Postgres keeps of a date.
    private static readonly TimeSpan DateStep = TimeSpan.FromMicroseconds(1);

    public async Task<StateIndexReport> UpdateAsync(
        StateIndexRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = timeProvider.GetTimestamp();
        try
        {
            var report = await LockedAsync(request, started, cancellationToken)
                .ConfigureAwait(false);
            var outcome = StateIndexMetrics.OutcomeOf(report.Outcome);
            metrics.RecordUpdate(request.Trigger, outcome, timeProvider.GetElapsedTime(started));
            return report;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = timeProvider.GetElapsedTime(started);
            metrics.RecordUpdate(request.Trigger, StateIndexMetrics.FailedOutcome, elapsed);
            throw;
        }
        finally
        {
            copy.Invalidate();
        }
    }

    public async Task<bool> HasCurrentAsync(CancellationToken cancellationToken) =>
        await store.CurrentAsync(cancellationToken).ConfigureAwait(false) is not null;

    private async Task<StateIndexReport> LockedAsync(
        StateIndexRequest request,
        long started,
        CancellationToken cancellationToken)
    {
        var wait = request.Trigger == StateIndexTrigger.Build ? BuildLockWait : LockWait;
        var held = await indexLock.AcquireAsync(wait, cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            StateIndexLog.Locked(logger, StateIndexMetrics.TriggerOf(request.Trigger));
            return new StateIndexReport { Outcome = StateIndexOutcome.Locked };
        }

        await using (held.ConfigureAwait(false))
        {
            var plan = await PlanAsync(request, cancellationToken).ConfigureAwait(false);
            var (outcome, cursor) = await WriteAsync(plan, cancellationToken).ConfigureAwait(false);
            return await ReportAsync(new Written(plan, outcome, cursor), started, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<UpdatePlan> PlanAsync(
        StateIndexRequest request,
        CancellationToken cancellationToken)
    {
        var current = await store.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var latest = await coverage.LatestAsync(cancellationToken).ConfigureAwait(false);
        if (latest is null)
        {
            return new UpdatePlan { Request = request, Current = current, Run = [] };
        }

        var listed = await coverage.ListedAsync(latest, cancellationToken).ConfigureAwait(false);
        var ingested = await coverage
            .IngestMissingAsync(Wanted(request, listed, current), cancellationToken)
            .ConfigureAwait(false);
        return new UpdatePlan
        {
            Request = request,
            Current = current,
            Run = await coverage.StoredRunAsync(listed, cancellationToken).ConfigureAwait(false),
            Ingested = ingested,
        };
    }

    // Every listed version when asked; else, for a promotion or the reconciliation, the
    // versions after the coverage end; nothing for a plain build.
    private static IEnumerable<PatchVersion> Wanted(
        StateIndexRequest request,
        IReadOnlyList<PatchVersion> listed,
        CatalogStateGeneration? current)
    {
        if (request.ShouldIngestMissing)
        {
            return listed;
        }

        return request.Trigger == StateIndexTrigger.Build || current?.LastVersion is not { } end
            ? []
            : listed.Where(version => version > PatchVersion.Parse(end));
    }

    private async Task<(StateIndexOutcome Outcome, GenerationCursor? Cursor)> WriteAsync(
        UpdatePlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Run.Count == 0)
        {
            return (StateIndexOutcome.Unchanged, null);
        }

        if (ShouldBuild(plan))
        {
            var built = await BuildAsync(plan, cancellationToken).ConfigureAwait(false);
            return (StateIndexOutcome.Built, built);
        }

        if (IsPastAGap(plan))
        {
            LogGap(plan);
            return (StateIndexOutcome.Unchanged, null);
        }

        var extended = await ExtendAsync(plan, cancellationToken).ConfigureAwait(false);
        return extended is null
            ? (StateIndexOutcome.Unchanged, null)
            : (StateIndexOutcome.Extended, extended);
    }

    // None current yet; or, for the command, a rebuild asked or a coverage start that moved
    // back. A start that moved forward is a listed version whose datasets are missing, not a
    // shorter history: a build from it would throw away every older version for good.
    private static bool ShouldBuild(UpdatePlan plan) =>
        plan.Current is not { } current
        || (plan.Request.Trigger == StateIndexTrigger.Build
            && (plan.Request.ShouldRebuild
                || plan.Run[0] < PatchVersion.Parse(current.FirstVersion)));

    // The run starts after the current end: a listed version in between lacks its en_US
    // datasets, and an extension would skip it. The generation stays as it is, behind, until
    // the promotion or the reconciliation ingests that version.
    private static bool IsPastAGap(UpdatePlan plan) =>
        plan.Current?.LastVersion is { } last && plan.Run[0] > PatchVersion.Parse(last);

    private void LogGap(UpdatePlan plan) =>
        StateIndexLog.Interrupted(
            logger,
            StateIndexMetrics.TriggerOf(plan.Request.Trigger),
            plan.Current!.Id,
            plan.Current.LastVersion!,
            plan.Run[0].Value);

    private async Task<GenerationCursor> BuildAsync(
        UpdatePlan plan,
        CancellationToken cancellationToken)
    {
        var generation = await PendingAsync(plan, cancellationToken).ConfigureAwait(false)
            ?? await store.CreateAsync(plan.Run[0], cancellationToken).ConfigureAwait(false);
        var closing = await ClosingAsync(plan.Current, generation.BuiltAt, cancellationToken)
            .ConfigureAwait(false);
        var cursor = await walker.ResumeAsync(generation, closing, cancellationToken)
            .ConfigureAwait(false);
        var versions = plan.After(cursor.Previous);
        await walker.WalkAsync(plan.Request.Trigger, cursor, versions, cancellationToken)
            .ConfigureAwait(false);
        await store.MakeCurrentAsync(generation.Id, plan.Current?.Id, cancellationToken)
            .ConfigureAwait(false);
        return cursor;
    }

    // The generation an interrupted rebuild left with the same coverage start resumes; any
    // other one a rebuild left goes.
    private async Task<CatalogStateGeneration?> PendingAsync(
        UpdatePlan plan,
        CancellationToken cancellationToken)
    {
        var pending = await store.PendingAsync(cancellationToken).ConfigureAwait(false);
        var resumed = plan.Request.ShouldRebuild
            ? null
            : pending.Find(generation => generation.FirstVersion == plan.Run[0].Value);
        var stale = pending.Where(generation => generation.Id != resumed?.Id)
            .Select(static generation => generation.Id)
            .ToList();
        await store.DeleteAsync(stale, cancellationToken).ConfigureAwait(false);
        return resumed;
    }

    // A rebuild dates the states the current generation closed alike as it did, and those it
    // closes past that generation's end, behind the promotions, after its own date.
    private async Task<ClosingDates> ClosingAsync(
        CatalogStateGeneration? current,
        DateTimeOffset rebuiltAt,
        CancellationToken cancellationToken)
    {
        if (current is null)
        {
            return ClosingDates.At(rebuiltAt);
        }

        var states = await store.StatesAsync(current.Id, cancellationToken).ConfigureAwait(false);
        var dates = ClosingDates.Keeping(
            states.Where(static state => state.Last is not null)
                .Select(static state => (ClosedOf(state), state.ClosedAt!.Value)),
            rebuiltAt);
        var walkedAt = StrictlyAfter(timeProvider.GetUtcNow(), rebuiltAt);
        return current.LastVersion is { } end
            ? dates.After(PatchVersion.Parse(end), walkedAt)
            : dates;
    }

    // Strictly after the rebuild's date, as stored, even on a clock that has not moved since.
    private static DateTimeOffset StrictlyAfter(DateTimeOffset moment, DateTimeOffset rebuiltAt) =>
        moment > rebuiltAt ? moment : rebuiltAt + DateStep;

    private async Task<GenerationCursor?> ExtendAsync(
        UpdatePlan plan,
        CancellationToken cancellationToken)
    {
        var current = plan.Current!;
        var end = current.LastVersion is { } last ? PatchVersion.Parse(last) : null;
        if (plan.After(end).Count == 0)
        {
            return null;
        }

        var closing = ClosingDates.At(timeProvider.GetUtcNow());
        var cursor = await walker.ResumeAsync(current, closing, cancellationToken)
            .ConfigureAwait(false);
        var versions = plan.After(cursor.Previous);
        await walker.WalkAsync(plan.Request.Trigger, cursor, versions, cancellationToken)
            .ConfigureAwait(false);
        return cursor;
    }

    private async Task<StateIndexReport> ReportAsync(
        Written written,
        long started,
        CancellationToken cancellationToken)
    {
        var generationId = written.Cursor?.GenerationId ?? written.Plan.Current?.Id;
        var summary = generationId is { } id
            ? await store.SummaryAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        StateIndexLog.Updated(logger, new UpdateLine
        {
            Trigger = StateIndexMetrics.TriggerOf(written.Plan.Request.Trigger),
            Outcome = StateIndexMetrics.OutcomeOf(written.Outcome),
            Summary = summary,
            Versions = written.Cursor?.Indexed ?? 0,
            Ingested = written.Plan.Ingested,
            Opened = written.Cursor?.Opened ?? 0,
            Closed = written.Cursor?.Closed ?? 0,
            DurationMs = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds,
        });
        return new StateIndexReport
        {
            Outcome = written.Outcome,
            GenerationId = generationId,
            CoverageStart = summary?.CoverageStart,
            CoverageEnd = summary?.CoverageEnd,
            VersionsIndexed = written.Cursor?.Indexed ?? 0,
        };
    }

    private static ClosedState ClosedOf(StoredState state) => new()
    {
        Resource = state.Resource,
        Key = state.Key,
        Opener = PatchVersion.Parse(state.Opener),
        Last = PatchVersion.Parse(state.Last!),
        Fingerprint = state.Fingerprint,
    };

    private sealed record Written(
        UpdatePlan Plan,
        StateIndexOutcome Outcome,
        GenerationCursor? Cursor);
}
