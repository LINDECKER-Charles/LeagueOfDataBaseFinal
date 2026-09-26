namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// One champion ability, with the per-rank values Data Dragon pre-formats ("8/7/6/5/4").
/// </summary>
/// <remarks>
/// Tooltips are left out on purpose: their <c>{{ }}</c> placeholders have no resolvable
/// values in Data Dragon. The range is kept raw; <c>RangeSentinels</c> masks placeholders.
/// </remarks>
public sealed record ChampionSpell
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Icon file name ("AhriQ.png").</summary>
    public required string Image { get; init; }

    public string? CooldownBurn { get; init; }

    public string? CostBurn { get; init; }

    public string? RangeBurn { get; init; }

    public int? MaxRank { get; init; }

    /// <summary>Raw ammo count; Data Dragon writes -1 for an ability without charges.</summary>
    public int? MaxAmmo { get; init; }

    /// <summary>Charges the ability stores, only when it has some.</summary>
    public int? Charges => MaxAmmo > 0 ? MaxAmmo : null;
}
