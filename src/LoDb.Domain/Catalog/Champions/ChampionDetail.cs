namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// A champion as its own dataset (<c>champion/{id}.json</c>) describes it.
/// </summary>
/// <remarks>
/// Every section beyond the summary may be missing on old versions or in some languages
/// (UP 3): each one is then empty, and a detail page still renders.
/// </remarks>
public sealed record ChampionDetail
{
    public required ChampionSummary Summary { get; init; }

    public string? Lore { get; init; }

    public required IReadOnlyList<string> AllyTips { get; init; }

    public required IReadOnlyList<string> EnemyTips { get; init; }

    /// <summary>Abilities in slot order (Q, W, E, R).</summary>
    public required IReadOnlyList<ChampionSpell> Spells { get; init; }

    public ChampionPassive? Passive { get; init; }

    public required IReadOnlyList<Skin> Skins { get; init; }

    /// <summary>A detail made of the summary alone, when the detail dataset is missing.</summary>
    public static ChampionDetail FromSummary(ChampionSummary summary) => new()
    {
        Summary = summary,
        AllyTips = [],
        EnemyTips = [],
        Spells = [],
        Skins = [],
    };
}
