using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Api.Modules.Catalog.States;
using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Editions;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items.Details;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/items/{id}</c>: the card, then the description, the
/// recipe, the upgrades and the champion the item is bound to, every image resolved, and the
/// items on either side of it in the list.
/// </summary>
internal sealed record ItemDetails
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

    /// <summary>The facts of the list's card, the edition and its twin among them.</summary>
    public required ItemCard Profile { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The map ids the item's availability line claims: the Classic Rift (453) alone for a
    /// Classic item, never for a current one (UP 6).
    /// </summary>
    public required IReadOnlyList<int> AvailableMaps { get; init; }

    /// <summary>The champion only who may buy it (Kalista's Black Spear).</summary>
    public EntityLink? RequiredChampion { get; init; }

    /// <summary>The ally the item needs in the team (Ornn's upgrades).</summary>
    public EntityLink? RequiredAlly { get; init; }

    /// <summary>The item and its components, recursively.</summary>
    public RecipeStep? Recipe { get; init; }

    /// <summary>
    /// Data Dragon's depth: 1 for a starter, 2 for an epic, 3 or 4 for a legendary built from
    /// epics; null when Data Dragon ships none (a consumable, a trinket).
    /// </summary>
    public int? Depth { get; init; }

    /// <summary>The listed items it builds into, in the upstream order, with their price.</summary>
    public required IReadOnlyList<ItemUpgrade> Upgrades { get; init; }

    /// <summary>The items before and after it in the list's order.</summary>
    public required DetailNeighbours Neighbours { get; init; }

    public static ItemDetails Of(ItemPage page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(images);
        var catalog = page.Catalog;
        var item = page.Item;
        var profile = ProfileOf(item, catalog, images);
        return new ItemDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Items.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Canonical = CanonicalLink.PageItself(catalog, profile.CanonicalPath),
            State = null,
            History = [],
            Next = null,
            PresentInLatest = false,
            Changes = null,
            Profile = profile,
            Description = DdragonText.Clean(item.Description),
            AvailableMaps = ItemEdition.ClaimableMapIds(item.Id, item.Maps),
            RequiredChampion = page.RequiredChampion is { } bound
                ? EntityLink.Of(bound, catalog, images)
                : null,
            RequiredAlly = page.RequiredAlly is { } ally
                ? EntityLink.Of(ally, catalog, images)
                : null,
            Recipe = page.Recipe is { } recipe ? RecipeStep.Of(recipe, page, images) : null,
            Depth = item.Depth,
            Upgrades =
                [.. page.Upgrades.Select(upgrade => ItemUpgrade.Of(upgrade, catalog, images))],
            Neighbours = ItemList.NeighboursOf(item, catalog),
        };
    }

    // The page prints the description in full below its hero: the hero's lead is the
    // plaintext alone, as on the legacy page, never that description a second time.
    private static ItemCard ProfileOf(Item item, CatalogSnapshot catalog, ImageSet images) =>
        ItemCard.Of(item, catalog, images) with { Summary = DdragonText.Clean(item.Plaintext) };

    /// <summary>The same page with what the index of states tells of it.</summary>
    public ItemDetails WithState(DetailState state)
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
