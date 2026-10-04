namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// A champion skin and its chromas.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is Riot's skin id ("103001"); <see cref="Number"/> is the index its art
/// files carry, already resolved by <c>SkinNumber</c> when an old version omits it (UP 3).
/// The name is kept raw: the default skin is named "default" and displays as the champion.
/// </remarks>
public sealed record Skin
{
    public required string Id { get; init; }

    public required int Number { get; init; }

    public required string Name { get; init; }

    /// <summary>Chromas from CommunityDragon; empty when that source has none.</summary>
    public required IReadOnlyList<Chroma> Chromas { get; init; }
}
