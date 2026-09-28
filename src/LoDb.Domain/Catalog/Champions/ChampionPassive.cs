namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// The innate ability of a champion.
/// </summary>
public sealed record ChampionPassive
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Icon file name ("Ahri_SoulEater2.png").</summary>
    public required string Image { get; init; }
}
