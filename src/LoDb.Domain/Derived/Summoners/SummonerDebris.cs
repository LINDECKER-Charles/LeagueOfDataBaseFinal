using LoDb.Domain.Catalog.Summoners;

namespace LoDb.Domain.Derived.Summoners;

/// <summary>
/// Riot's placeholder summoner spells, cleaned in one place so that the list, the search,
/// the counts, the sitemap and the detail lookup all agree, as for items (<c>ItemDebris</c>).
/// </summary>
/// <remarks>
/// Data Dragon ships "Summoner_UltBookPlaceholder" and "Summoner_UltBookSmitePlaceholder"
/// (ULTBOOK mode, no cooldown): slots the game fills with another champion's ultimate, not
/// spells a player can pick. The marker is read in the id, which is language-independent,
/// and in the en_US name of the same version, because Data Dragon translates the name in
/// some languages.
/// </remarks>
public static class SummonerDebris
{
    private const string PlaceholderMarker = "Placeholder";

    /// <summary>Whether the spell is a placeholder rather than an encyclopedia entry.</summary>
    /// <param name="spell">The spell, in any language.</param>
    /// <param name="englishName">
    /// The en_US name of the same id and version; <see langword="null"/> when en_US lacks the
    /// id, the marker then read in the spell's own name.
    /// </param>
    public static bool IsDebris(SummonerSpell spell, string? englishName)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.Id.Contains(PlaceholderMarker, StringComparison.Ordinal)
            || (englishName ?? spell.Name).Contains(PlaceholderMarker, StringComparison.Ordinal);
    }
}
