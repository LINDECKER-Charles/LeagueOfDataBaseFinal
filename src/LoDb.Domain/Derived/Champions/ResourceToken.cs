using System.Buffers;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Text;

namespace LoDb.Domain.Derived.Champions;

/// <summary>
/// Language-independent token of a champion's resource ("mana", "energy", "none").
/// </summary>
/// <remarks>
/// <c>partype</c> is localized ("Energy", "Énergie", "에너지"), so the token is read from the
/// en_US entry of the same champion id (UP 13): a shared filter URL must mean the same thing in
/// every locale. Versions without <c>partype</c> read as "none" (UP 3).
/// </remarks>
public static class ResourceToken
{
    /// <summary>Token of a champion without resource, or of a version that ships none.</summary>
    public const string None = "none";

    private static readonly SearchValues<char> NothingSkipped = SearchValues.Create(string.Empty);

    /// <param name="english">The en_US entry of the champion, when en_US carries it.</param>
    /// <param name="localized">The entry in the language being read.</param>
    public static string Of(ChampionSummary? english, ChampionSummary localized)
    {
        ArgumentNullException.ThrowIfNull(localized);
        var partype = (english ?? localized).Partype ?? string.Empty;
        var token = AsciiCase.Hyphenate(partype, NothingSkipped);
        return token.Length == 0 ? None : token;
    }
}
