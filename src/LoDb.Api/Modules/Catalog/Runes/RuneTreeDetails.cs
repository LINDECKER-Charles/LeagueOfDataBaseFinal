using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Api.Modules.Catalog.States;
using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Derived.Runes;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/runes/{id}</c>: a rune path and its rows, every image
/// resolved, and the paths on either side of it in the list.
/// </summary>
internal sealed record RuneTreeDetails
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    /// <summary>The page the routing redirects a stale or slugless URL to.</summary>
    public required string CanonicalPath { get; init; }

    /// <summary>
    /// The page search engines index for the entity: never null, the page itself when no
    /// other is.
    /// </summary>
    public required CanonicalLink Canonical { get; init; }

    /// <summary>
    /// The state that holds the page's version; null outside the index (a version older than
    /// it, listed after the latest, or before any promotion).
    /// </summary>
    public required EntityState? State { get; init; }

    /// <summary>
    /// The entity's states up to the page's, newest first; every state on the short URL.
    /// </summary>
    public required IReadOnlyList<StateHistoryEntry> History { get; init; }

    /// <summary>What followed the page's state, when it is closed; null otherwise.</summary>
    public required StateNext? Next { get; init; }

    /// <summary>
    /// Whether the latest version holds the entity (« Dans le dernier patch »).
    /// </summary>
    public required bool PresentInLatest { get; init; }

    /// <summary>
    /// What the page's state changed: on its opener, and on the short URL when the open state
    /// opened at the latest version; null otherwise, and for a state of kind baseline.
    /// </summary>
    public required EntityChanges? Changes { get; init; }

    public required RuneTreeCard Profile { get; init; }

    public required IReadOnlyList<RuneRow> Slots { get; init; }

    /// <summary>The paths before and after it in the list's order.</summary>
    public required DetailNeighbours Neighbours { get; init; }

    public static RuneTreeDetails Of(RuneTree tree, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(catalog);
        var profile = RuneTreeCard.Of(tree, catalog, images);
        return new RuneTreeDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Runes.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Canonical = CanonicalLink.PageItself(catalog, profile.CanonicalPath),
            State = null,
            History = [],
            Next = null,
            PresentInLatest = false,
            Changes = null,
            Profile = profile,
            Slots = [.. tree.Slots.Select((slot, index) => new RuneRow
            {
                Slot = RuneSlotToken.Of(index),
                Runes = [.. slot.Runes.Select(rune => RuneEntry.Of(rune, images))],
            })],
            Neighbours = RuneList.NeighboursOf(tree, catalog),
        };
    }

    /// <summary>The same page with what the index of states tells of it.</summary>
    public RuneTreeDetails WithState(DetailState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return this with
        {
            Canonical = state.Canonical,
            State = state.State,
            History = state.History,
            Next = state.Next,
            PresentInLatest = state.PresentInLatest,
            Changes = state.Changes,
        };
    }
}
