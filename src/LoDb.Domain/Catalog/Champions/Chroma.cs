namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// A color variant of a skin, as CommunityDragon describes it.
/// </summary>
/// <remarks>
/// Data Dragon only flags that a skin has chromas; their names, colors and swatches come
/// from CommunityDragon. Data Dragon also lists each chroma as a skin of its own, which
/// <c>ChromaSkins</c> removes once a parent skin claims it (UP 9).
/// </remarks>
public sealed record Chroma
{
    /// <summary>Riot's id, the same as the id of the skin entry Data Dragon duplicates.</summary>
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Hex colors of the variant, main color first ("#0AC8B9").</summary>
    public required IReadOnlyList<string> Colors { get; init; }

    /// <summary>
    /// CommunityDragon asset path of the swatch, the whole art of a chroma: a chroma without
    /// one is not kept.
    /// </summary>
    public required string Image { get; init; }
}
