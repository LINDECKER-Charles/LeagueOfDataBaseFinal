using LoDb.Domain.Editions;

namespace LoDb.Domain.Catalog.Items;

/// <summary>
/// An item as <c>item.json</c> describes it, keyed by its id ("3078").
/// </summary>
/// <remarks>
/// The edition follows from the id alone (UP 6), so it is computed rather than stored and can
/// never disagree with it. The twin needs the whole dataset: <c>EditionTwins</c> sets it.
/// </remarks>
public sealed record Item
{
    public required string Id { get; init; }

    /// <summary>
    /// Display name: Arena variants ship markup in their names, reduced by
    /// <c>ItemDebris.DisplayName</c> before an item is built.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Rich description, with Data Dragon's markup kept for the renderer.</summary>
    public required string Description { get; init; }

    public required string Plaintext { get; init; }

    /// <summary>Icon file name ("3078.png").</summary>
    public required string Image { get; init; }

    public required ItemGold Gold { get; init; }

    /// <summary>Ids of the components, in recipe order.</summary>
    public required IReadOnlyList<string> From { get; init; }

    /// <summary>Ids of the items this one builds into.</summary>
    public required IReadOnlyList<string> Into { get; init; }

    /// <summary>Recipe depth; missing on base components.</summary>
    public int? Depth { get; init; }

    /// <summary>Raw stats under their Data Dragon keys ("FlatHPPoolMod"…).</summary>
    public required IReadOnlyDictionary<string, double> Stats { get; init; }

    /// <summary>Availability per Data Dragon map id; a missing flag excludes nothing.</summary>
    public required IReadOnlyDictionary<int, bool> Maps { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public bool IsConsumed { get; init; }

    public bool IsHiddenFromAll { get; init; }

    /// <summary>Id of the only champion allowed to own the item (Kalista's spear).</summary>
    public string? RequiredChampion { get; init; }

    /// <summary>Id of the ally the item requires (Ornn's upgrades).</summary>
    public string? RequiredAlly { get; init; }

    public Edition Edition => ItemEdition.Of(Id);

    /// <summary>The same-named item of the other game, when the dataset carries it.</summary>
    public EditionTwin? Counterpart { get; init; }
}
