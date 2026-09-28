using System.Collections.Frozen;
using LoDb.Domain.Editions;

namespace LoDb.Domain.Catalog.Modes;

/// <summary>
/// The one curated list of summoner-spell game modes worth showing, with Riot's product names.
/// </summary>
/// <remarks>
/// Data Dragon mixes real queues with internal ids (WIPMODEWIP, RUBY_TRIAL_1,
/// TUTORIAL_MODULE_1…): anything outside this list is never displayed nor filterable (UP 11).
/// CLASSIC is Data Dragon's id of the standard Summoner's Rift queue, not to be confused with
/// JADE, the LoL Classic client: an edition, labelled by the interface translations.
/// </remarks>
public static class GameModeLabels
{
    // Display order of the list facet.
    private static readonly IReadOnlyList<(string Mode, string Label)> Labels =
    [
        ("CLASSIC", "Summoner's Rift"),
        ("ARAM", "ARAM"),
        ("CHERRY", "Arena"),
        ("BRAWL", "Brawl"),
        ("NEXUSBLITZ", "Nexus Blitz"),
        ("URF", "URF"),
        ("SNOWURF", "Snow ARURF"),
        ("ARSR", "ARSR"),
        ("ONEFORALL", "One for All"),
        ("ULTBOOK", "Ultimate Spellbook"),
        ("SWIFTPLAY", "Swiftplay"),
        ("KINGPORO", "Legend of the Poro King"),
        ("ASSASSINATE", "Blood Moon"),
        ("PRACTICETOOL", "Practice Tool"),
        ("TUTORIAL", "Tutorial"),
    ];

    private static readonly FrozenDictionary<string, string> LabelByMode =
        Labels.ToFrozenDictionary(
            entry => entry.Mode,
            entry => entry.Label,
            StringComparer.Ordinal);

    // Shown on a spell's plaque, but nobody filters spells for them.
    private static readonly FrozenSet<string> NotFacetable =
        FrozenSet.Create(StringComparer.Ordinal, "PRACTICETOOL", "TUTORIAL");

    /// <summary>Modes the list facet offers, in display order.</summary>
    public static IReadOnlyList<string> Facetable { get; } =
        [.. Labels.Select(entry => entry.Mode).Where(mode => !NotFacetable.Contains(mode))];

    /// <summary>
    /// Product name of a displayable mode; <see langword="null"/> for an internal id.
    /// </summary>
    public static string? LabelOf(string mode) => LabelByMode.GetValueOrDefault(mode);

    public static bool IsFacetable(string mode) =>
        LabelByMode.ContainsKey(mode) && !NotFacetable.Contains(mode);

    /// <summary>
    /// Modes of a spell worth displaying, input order kept, duplicates and internal ids
    /// dropped. JADE stays: the interface labels it as the LoL Classic edition.
    /// </summary>
    public static IReadOnlyList<string> Displayable(IEnumerable<string> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return [.. modes.Where(IsDisplayable).Distinct(StringComparer.Ordinal)];
    }

    private static bool IsDisplayable(string mode) =>
        LabelByMode.ContainsKey(mode)
        || string.Equals(mode, SummonerSpellEdition.ClassicMode, StringComparison.Ordinal);
}
