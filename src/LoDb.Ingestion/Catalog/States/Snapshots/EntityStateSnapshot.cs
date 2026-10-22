using System.Collections.Frozen;
using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>
/// An immutable copy of a generation of the index of states, queried in memory: the
/// one place that says which state holds a page, what a version changed, and how to date it.
/// </summary>
/// <remarks>
/// Built from rows, pure: no query reads a catalog or the database. Lists come in a stable
/// order, by resource then key unless said otherwise, so that the files built from them do not
/// change between two reads of the same generation.
/// </remarks>
public sealed class EntityStateSnapshot
{
    private readonly FrozenDictionary<(ResourceType, string), IndexedState[]> byEntity;
    private readonly FrozenDictionary<PatchVersion, IndexedState[]> byOpener;
    private readonly FrozenDictionary<PatchVersion, IndexedState[]> byLast;
    private readonly PromotedVersion[] promotions;

    private EntityStateSnapshot(
        StateIndexGeneration? generation,
        IReadOnlyList<IndexedState> states,
        IReadOnlyList<PromotedVersion> promoted)
    {
        Generation = generation;
        Coverage = generation is null ? CoveredVersions.None : new(generation.Versions);
        States = [.. states.OrderBy(static state => state.Resource)
            .ThenBy(static state => state.Key, StringComparer.Ordinal)
            .ThenBy(static state => state.Opener)];
        byEntity = States.GroupBy(static state => (state.Resource, state.Key))
            .ToFrozenDictionary(static group => group.Key, static group => group.ToArray());
        byOpener = States.GroupBy(static state => state.Opener)
            .ToFrozenDictionary(static group => group.Key, static group => group.ToArray());
        byLast = States.Where(static state => state.Last is not null)
            .GroupBy(static state => state.Last!)
            .ToFrozenDictionary(static group => group.Key, static group => group.ToArray());
        promotions = [.. promoted.OrderBy(static promotion => promotion.Version)];
    }

    /// <summary>No generation is current: every page takes the residual rule.</summary>
    public static EntityStateSnapshot Empty { get; } = new(null, [], []);

    /// <summary>The generation read; null for <see cref="Empty"/>.</summary>
    public StateIndexGeneration? Generation { get; }

    public CoveredVersions Coverage { get; }

    /// <summary>What a reader compares to know whether its copy is stale.</summary>
    public StateIndexStamp? Stamp =>
        Generation is null ? null : new StateIndexStamp(Generation.Id, Coverage.End);

    /// <summary>Every state, by resource, key, then opener.</summary>
    public IReadOnlyList<IndexedState> States { get; }

    /// <param name="generation">The generation, its covered versions oldest first.</param>
    /// <param name="states">Its states.</param>
    /// <param name="promoted">The promoted versions, which date the patch pages.</param>
    public static EntityStateSnapshot Of(
        StateIndexGeneration generation,
        IReadOnlyList<IndexedState> states,
        IReadOnlyList<PromotedVersion> promoted)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(promoted);
        return new EntityStateSnapshot(generation, states, promoted);
    }

    /// <summary>The entity's states, oldest first; none for an entity never indexed.</summary>
    public IReadOnlyList<IndexedState> StatesOf(IndexedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return byEntity.GetValueOrDefault((entity.Resource, entity.Key)) ?? [];
    }

    /// <summary>
    /// The state that decides the page of <paramref name="entity"/> at
    /// <paramref name="version"/>, the latest version being the catalog reader's.
    /// </summary>
    public StateLocation Locate(IndexedEntity entity, PatchVersion version, PatchVersion? latest)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (latest is null || version > latest || Coverage.End is not { } end)
        {
            return StateLocation.Outside;
        }

        var states = StatesOf(entity);
        if (Coverage.Contains(version))
        {
            return new StateLocation
            {
                Kind = StateLocationKind.Covered,
                State = states.FirstOrDefault(state => state.Holds(version)),
            };
        }

        return version > end && states.Count > 0 && states[^1] is { IsOpen: true } open
            ? new StateLocation { Kind = StateLocationKind.Lagging, State = open }
            : StateLocation.Outside;
    }

    /// <summary>
    /// How <paramref name="state"/> begins as its pages tell it: an entity's first state opened
    /// at the coverage start is <see cref="EntityChangeKind.Baseline"/>, compared to nothing
    /// (it may be older than the coverage); any other, the state's own kind.
    /// </summary>
    public EntityChangeKind KindOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PreviousOpener is null && state.Opener == Coverage.Start
            ? EntityChangeKind.Baseline
            : state.Kind;
    }

    /// <summary>The entity's state after <paramref name="state"/>; null for its last one.</summary>
    public IndexedState? NextOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var states = byEntity.GetValueOrDefault((state.Resource, state.Key)) ?? [];
        var position = Array.FindIndex(states, held => held.Opener == state.Opener);
        return position >= 0 && position < states.Length - 1 ? states[position + 1] : null;
    }

    /// <summary>
    /// The covered version that no longer holds the entity of a closed state: the version
    /// after its last one, unless the entity's next state opens there (a change, not a
    /// retirement). Null for an open state, or one closed by a change.
    /// </summary>
    public PatchVersion? RetirementOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Last is not { } last || Coverage.After(last) is not { } after)
        {
            return null;
        }

        return NextOf(state)?.Opener == after ? null : after;
    }

    /// <summary>The states opened at <paramref name="version"/>, open and closed alike.</summary>
    public IReadOnlyList<IndexedState> OpenedAt(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return byOpener.GetValueOrDefault(version) ?? [];
    }

    /// <summary>
    /// The closed states opened at <paramref name="version"/>: the openers its sitemap file
    /// lists, whose pages are frozen and their own canonical.
    /// </summary>
    public IReadOnlyList<IndexedState> ClosedOpenedAt(PatchVersion version) =>
        [.. OpenedAt(version).Where(static state => !state.IsOpen)];

    /// <summary>
    /// The last states of the entities <paramref name="version"/> no longer holds, which the
    /// version before it did.
    /// </summary>
    public IReadOnlyList<IndexedState> RetiredAt(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Coverage.Before(version) is { } before
            && byLast.GetValueOrDefault(before) is { } closed
            ? [.. closed.Where(state => RetirementOf(state) == version)]
            : [];
    }

    /// <summary>
    /// Whether <paramref name="version"/> has a patch page: a covered version but the oldest
    /// one, which has no version before it to compare to.
    /// </summary>
    public bool HasPatchPage(PatchVersion version) =>
        Coverage.Contains(version) && version != Coverage.Start;

    /// <summary>
    /// Whether the patch page of <paramref name="version"/> shows anything: a state opened
    /// there, or an entity retired there. One without is <c>noindex</c>, and out of the
    /// sitemaps and of IndexNow.
    /// </summary>
    public bool HasChanges(PatchVersion version) =>
        HasPatchPage(version) && (OpenedAt(version).Count > 0 || RetiredAt(version).Count > 0);

    /// <summary>
    /// The closed states whose last version lies from <paramref name="from"/> on, before
    /// <paramref name="until"/>: with the previous coverage end and the promoted version, the
    /// states that promotion closed. By last version, then resource and key.
    /// </summary>
    public IReadOnlyList<IndexedState> ClosedBetween(PatchVersion from, PatchVersion until)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(until);
        return [.. byLast
            .Where(pair => pair.Key >= from && pair.Key < until)
            .OrderBy(static pair => pair.Key)
            .SelectMany(static pair => pair.Value)];
    }

    /// <summary>
    /// The newest version promoted before <paramref name="version"/>: the latest patch its
    /// promotion replaced, whatever was listed between them without being promoted. Null
    /// before any earlier promotion.
    /// </summary>
    public PatchVersion? PromotedBefore(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return promotions.LastOrDefault(promotion => promotion.Version < version)?.Version;
    }

    /// <summary>
    /// The <c>lastmod</c> of the patch page of <paramref name="version"/>: the promotion
    /// that made the version an older one, else the generation's build when it is later (a
    /// rebuilt history). Null without a patch page.
    /// </summary>
    public DateTimeOffset? PatchPageModifiedAt(PatchVersion version)
    {
        if (Generation is not { } generation || !HasPatchPage(version))
        {
            return null;
        }

        var leftAt = promotions.FirstOrDefault(promotion => promotion.Version > version)
            ?.PromotedAt;
        return leftAt is { } promoted && promoted > generation.BuiltAt
            ? promoted
            : generation.BuiltAt;
    }

    /// <summary>
    /// The <c>lastmod</c> of the sitemap file of <paramref name="version"/>: the newest of its
    /// pages, the patch page when it changed something and the openers of its closed states.
    /// Null when the file lists nothing.
    /// </summary>
    public DateTimeOffset? VersionFileModifiedAt(PatchVersion version)
    {
        var patchPage = HasChanges(version) ? PatchPageModifiedAt(version) : null;
        return ClosedOpenedAt(version)
            .Select(static state => state.ClosedAt)
            .Append(patchPage)
            .Max();
    }
}
