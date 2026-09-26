using LoDb.Domain.Editions;

namespace LoDb.Domain.Catalog.Summoners;

/// <summary>
/// A summoner spell as <c>summoner.json</c> describes it, keyed by its id ("SummonerFlash").
/// </summary>
/// <remarks>
/// The edition follows from the id and the modes (UP 6), so it is computed rather than
/// stored. The twin needs the whole dataset: <c>EditionTwins</c> sets it.
/// </remarks>
public sealed record SummonerSpell
{
    public required string Id { get; init; }

    /// <summary>Numeric id the game uses ("4").</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Icon file name ("SummonerFlash.png").</summary>
    public required string Image { get; init; }

    /// <summary>Cooldown per rank, in seconds.</summary>
    public required IReadOnlyList<double> Cooldown { get; init; }

    public required IReadOnlyList<double> Cost { get; init; }

    /// <summary>Range per rank; 25000 and beyond means global (<c>RangeSentinels</c>).</summary>
    public required IReadOnlyList<double> Range { get; init; }

    /// <summary>Resource paid, in the dataset language ("No Cost" when free).</summary>
    public string? CostType { get; init; }

    /// <summary>Summoner level that unlocks the spell.</summary>
    public int? SummonerLevel { get; init; }

    /// <summary>Raw ammo count; Data Dragon writes -1 for a spell without charges.</summary>
    public int? MaxAmmo { get; init; }

    /// <summary>Charges the spell stores, only when it has some.</summary>
    public int? Charges => MaxAmmo > 0 ? MaxAmmo : null;

    /// <summary>Data Dragon game modes the spell is played in ("CLASSIC", "ARAM"…).</summary>
    public required IReadOnlyList<string> Modes { get; init; }

    public Edition Edition => SummonerSpellEdition.Of(Id, Modes);

    /// <summary>The same-named spell of the other game, when the dataset carries it.</summary>
    public EditionTwin? Counterpart { get; init; }
}
