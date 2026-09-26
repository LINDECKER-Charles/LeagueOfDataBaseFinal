namespace LoDb.Domain.Editions;

/// <summary>
/// Game an item or a summoner spell belongs to: today's League of Legends, or LoL Classic.
/// </summary>
/// <remarks>
/// Since LoL Classic shipped through Data Dragon (mode JADE, map 453), item.json and
/// summoner.json carry classic twins under the same display names ("Faerie Charm" is both
/// 1004 and 771004): the edition tells them apart wherever a name used to be enough (UP 6).
/// Champions and runes have no classic entries, so they carry no edition.
/// </remarks>
public enum Edition
{
    Modern,
    Classic,
}
