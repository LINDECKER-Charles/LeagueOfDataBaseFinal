using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Catalog.States.Canonicals;

/// <summary>
/// The index of states as one answer reads it, with the latest version and its catalog: the
/// single rule of the canonicals of the entity pages (ADR 0005, amendment of 2026-10-22).
/// </summary>
/// <remarks>
/// <para>
/// The latest version is the catalog reader's, never the index's: a page at that version is
/// its own canonical whatever the index says. A pinned page the index covers takes its state's
/// canonical: the opener of a closed state, the short URL while it is open; a version past the
/// coverage end, the index lagging a promotion, stands in the entity's open state.
/// </para>
/// <para>
/// Outside the index (a version older than its coverage, one listed after the latest, before
/// any promotion), the residual rule decides: the short URL when the latest catalog holds the
/// entity, else the page itself. Paths and presence do not depend on the language: the en_US
/// catalog of the latest version answers for every locale.
/// </para>
/// </remarks>
public sealed class StateView
{
    private readonly CatalogSnapshot? latestCatalog;

    /// <param name="snapshot">The index read for the answer.</param>
    /// <param name="latest">The catalog reader's latest version; null before any promotion.</param>
    /// <param name="latestCatalog">
    /// A catalog of <paramref name="latest"/>, which tells the entity's path there; null before
    /// any promotion.
    /// </param>
    public StateView(
        EntityStateSnapshot snapshot,
        PatchVersion? latest,
        CatalogSnapshot? latestCatalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (latestCatalog is not null && latestCatalog.Version != latest)
        {
            throw new ArgumentException(
                "The catalog is not the latest one.",
                nameof(latestCatalog));
        }

        Snapshot = snapshot;
        Latest = latest;
        this.latestCatalog = latestCatalog;
    }

    public EntityStateSnapshot Snapshot { get; }

    public PatchVersion? Latest { get; }

    /// <summary>
    /// Where the page of <paramref name="path"/> at <paramref name="version"/> stands.
    /// </summary>
    public PageStanding Locate(CanonicalPath path, PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(version);
        var entity = new IndexedEntity(path.Type, path.Id);
        var location = Snapshot.Locate(entity, version, Latest);
        var latestPath = LatestPathOf(path);
        var isLatest = version == Latest;
        return new PageStanding
        {
            Entity = entity,
            Version = version,
            Location = location,
            IsLatest = isLatest,
            IsPresentInLatest = isLatest || latestPath is not null,
            Canonical = isLatest ? CanonicalTarget.ShortUrl(path.Value)
                : location.State is { } state ? LinkOf(state, path)
                : Residual(path, version, latestPath),
        };
    }

    /// <summary>
    /// The canonical of a state of the entity <paramref name="path"/> names: its opener once
    /// closed; while open, the short URL, or its opener should the latest version lack it.
    /// </summary>
    public CanonicalTarget LinkOf(IndexedState state, CanonicalPath path)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.IsOpen ? ShortUrlOf(state, path) : CanonicalTarget.OpenerOf(state);
    }

    /// <summary>
    /// The opener's page as « Inchangé depuis » links it: its versioned URL, the short URL
    /// when the opener is the latest version.
    /// </summary>
    public CanonicalTarget OpenerLinkOf(IndexedState state, CanonicalPath path)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Opener == Latest ? ShortUrlOf(state, path) : CanonicalTarget.OpenerOf(state);
    }

    /// <summary>
    /// The entity's states up to the page's included, newest first; none outside the index.
    /// </summary>
    public IReadOnlyList<IndexedState> HistoryOf(PageStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);
        if (standing.State is not { } held)
        {
            return [];
        }

        return [.. Snapshot.StatesOf(standing.Entity)
            .Where(state => state.Opener <= held.Opener)
            .Reverse()];
    }

    /// <summary>What ended a closed state; null for an open one.</summary>
    public StateSuccessor? SuccessorOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Last is not { } last)
        {
            return null;
        }

        if (Snapshot.NextOf(state) is { } next && next.Opener == Snapshot.Coverage.After(last))
        {
            return new StateSuccessor(next.Opener, next);
        }

        return Snapshot.RetirementOf(state) is { } retired
            ? new StateSuccessor(retired, null)
            : null;
    }

    /// <summary>
    /// The state a state's changes are read against: the entity's previous one, whose last
    /// version is compared, a return included; null for a new entity.
    /// </summary>
    public IndexedState? PreviousOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PreviousOpener is { } opener
            ? Snapshot.StatesOf(new IndexedEntity(state.Resource, state.Key))
                .FirstOrDefault(previous => previous.Opener == opener)
            : null;
    }

    private CanonicalTarget ShortUrlOf(IndexedState state, CanonicalPath path) =>
        LatestPathOf(path) is { } latest
            ? CanonicalTarget.ShortUrl(latest.Value)
            : CanonicalTarget.OpenerOf(state);

    private static CanonicalTarget Residual(
        CanonicalPath path,
        PatchVersion version,
        CanonicalPath? latestPath) =>
        latestPath is { } latest
            ? CanonicalTarget.ShortUrl(latest.Value)
            : CanonicalTarget.Pinned(version, path.Value);

    private CanonicalPath? LatestPathOf(CanonicalPath path) => latestCatalog?.PageOf(path);
}
