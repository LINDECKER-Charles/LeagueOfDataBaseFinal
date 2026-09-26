namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// Riot's 0-10 ratings of a champion (<c>info</c> in the dataset).
/// </summary>
public sealed record ChampionRatings
{
    public required int Attack { get; init; }

    public required int Defense { get; init; }

    public required int Magic { get; init; }

    public required int Difficulty { get; init; }
}
