using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Building;
using LoDb.Ingestion.Catalog.States.Storage;
using LoDb.Ingestion.Catalog.States.Telemetry;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>
/// Walks versions into a generation, one en_US catalog at a time: each is compared to the
/// covered version before it, then written in its own transaction.
/// </summary>
/// <remarks>
/// A version whose four datasets are definitively absent is skipped, as if unlisted. A walk
/// that resumes reads its tracks back from the stored states, projected at the version written
/// last; a state that comes back, or whose entity that version lacked, is projected from its
/// opener's catalog when a version needs to compare to it.
/// </remarks>
internal sealed class GenerationWalker(
    EnglishEntities english,
    GenerationStore store,
    VersionWriter writer,
    StateIndexMetrics metrics,
    ILogger<GenerationWalker> logger)
{
    private const int ProgressInterval = 50;

    /// <summary>A cursor after the version <paramref name="generation"/> covers last.</summary>
    public async Task<GenerationCursor> ResumeAsync(
        CatalogStateGeneration generation,
        ClosingDates closing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var previous = generation.LastVersion is { } last ? PatchVersion.Parse(last) : null;
        var states = previous is null
            ? []
            : await store.StatesAsync(generation.Id, cancellationToken).ConfigureAwait(false);
        var cursor = new GenerationCursor
        {
            GenerationId = generation.Id,
            Tracks = StateTracks.Of(states.Select(TrackOf)),
            Closing = closing,
            Previous = previous,
            NextOrdinal = await store.VersionCountAsync(generation.Id, cancellationToken)
                .ConfigureAwait(false),
        };
        if (previous is not null
            && await english.ReadAsync(previous, cancellationToken).ConfigureAwait(false) is
                { } entities)
        {
            cursor.Tracks.Project(entities);
        }

        return cursor;
    }

    /// <summary>Indexes <paramref name="versions"/>, oldest first, after the cursor.</summary>
    public async Task WalkAsync(
        StateIndexTrigger trigger,
        GenerationCursor cursor,
        IReadOnlyList<PatchVersion> versions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(versions);
        foreach (var version in versions)
        {
            var entities = await ReadAsync(version, cancellationToken).ConfigureAwait(false);
            if (entities.IsEmpty)
            {
                continue;
            }

            await ProjectAsync(cursor.Tracks, entities, cancellationToken).ConfigureAwait(false);
            var changes = cursor.Tracks.Advance(entities, cursor.Previous);
            await writer.WriteAsync(cursor, changes, cancellationToken).ConfigureAwait(false);
            cursor.Advance(changes);
            Record(trigger, cursor, changes);
        }
    }

    private static EntityTrack TrackOf(StoredState state) => new()
    {
        Resource = state.Resource,
        Key = state.Key,
        LastOpener = PatchVersion.Parse(state.Opener),
        Fingerprint = state.Fingerprint,
        IsOpen = state.Last is null,
    };

    private async Task<VersionEntities> ReadAsync(
        PatchVersion version,
        CancellationToken cancellationToken) =>
        await english.ReadAsync(version, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            $"The en_US datasets of {version.Value} are not stored: it cannot be indexed.");

    // The states the version compares to and the walk has not projected, read back from the
    // catalogs of their openers, one catalog per opener.
    private async Task ProjectAsync(
        StateTracks tracks,
        VersionEntities entities,
        CancellationToken cancellationToken)
    {
        foreach (var opener in tracks.Unprojected(entities).GroupBy(track => track.LastOpener))
        {
            var then = await ReadAsync(opener.Key, cancellationToken).ConfigureAwait(false);
            foreach (var track in opener)
            {
                track.Projection = then.Find(track.Resource, track.Key)?.Projection
                    ?? throw new InvalidOperationException(
                        $"{track.Key} has no page in {opener.Key.Value}, its state's opener.");
            }
        }
    }

    private void Record(StateIndexTrigger trigger, GenerationCursor cursor, VersionChanges changes)
    {
        metrics.RecordVersion(
            trigger,
            [.. changes.Opened.Select(static state => state.Kind)],
            changes.Closed.Count);
        if (cursor.Indexed % ProgressInterval == 0)
        {
            var tag = StateIndexMetrics.TriggerOf(trigger);
            StateIndexLog.Progress(
                logger,
                tag,
                cursor.GenerationId,
                changes.Version.Value,
                cursor.Indexed);
        }
    }
}
