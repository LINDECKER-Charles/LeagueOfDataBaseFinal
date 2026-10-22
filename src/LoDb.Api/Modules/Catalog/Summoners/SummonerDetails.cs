using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Api.Modules.Catalog.States;
using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Derived.Ranges;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/summoners/{id}</c>: the card, then the cost, range
/// and charges, the image resolved, and the spells on either side of it in the list.
/// </summary>
internal sealed record SummonerDetails
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
    /// opened at the latest version; null otherwise.
    /// </summary>
    public required EntityChanges? Changes { get; init; }

    /// <summary>The facts of the list's card, the edition and its twin among them.</summary>
    public required SummonerCard Profile { get; init; }

    public required IReadOnlyList<double> Cost { get; init; }

    /// <summary>What the cost is paid in, when Data Dragon says.</summary>
    public string? CostType { get; init; }

    /// <summary>Range per rank; see <see cref="GlobalRange"/>.</summary>
    public required IReadOnlyList<double> Range { get; init; }

    /// <summary>Whether the range is Data Dragon's "whole map" sentinel.</summary>
    public required bool GlobalRange { get; init; }

    public int? Charges { get; init; }

    /// <summary>The spells before and after it in the list's order.</summary>
    public required DetailNeighbours Neighbours { get; init; }

    public static SummonerDetails Of(
        SummonerSpell spell,
        CatalogSnapshot catalog,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(catalog);
        var profile = SummonerCard.Of(spell, catalog, images);
        return new SummonerDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Summoners.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Canonical = CanonicalLink.PageItself(catalog, profile.CanonicalPath),
            State = null,
            History = [],
            Next = null,
            PresentInLatest = false,
            Changes = null,
            Profile = profile,
            Cost = spell.Cost,
            CostType = spell.CostType,
            Range = spell.Range,
            GlobalRange = spell.Range.Any(RangeSentinels.IsGlobal),
            Charges = spell.Charges,
            Neighbours = SummonerList.NeighboursOf(spell, catalog),
        };
    }

    /// <summary>The same page with what the index of states tells of it.</summary>
    public SummonerDetails WithState(DetailState state)
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
