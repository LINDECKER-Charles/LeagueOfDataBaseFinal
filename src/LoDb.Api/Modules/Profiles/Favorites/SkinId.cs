using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>
/// The favorite skin a profile shows as its banner, stored as <c>{championId}_{number}</c>
/// (<c>Ahri_7</c>): the stem of the Data Dragon splash, so its art derives from the id alone.
/// </summary>
/// <param name="ChampionId">The champion, such as <c>Ahri</c>.</param>
/// <param name="Number">The skin number, 0 for the base skin.</param>
internal sealed partial record SkinId(string ChampionId, int Number)
{
    /// <summary>Length of <c>users.favorite_skin_id</c>.</summary>
    public const int MaxLength = 64;

    private const char Separator = '_';
    private const char BaseNumber = '0';

    // A champion id is alphanumeric; a skin number is small.
    private const string Pattern = "^[A-Za-z0-9]+_[0-9]{1,4}\\z";

    /// <summary>
    /// Whether <paramref name="text"/> is a skin id: the only check a save makes, since a bad
    /// id only costs its art a 404 and a save must not depend on the catalog.
    /// </summary>
    public static bool IsWellFormed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= MaxLength && Syntax().IsMatch(text);
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out SkinId? skin)
    {
        skin = null;
        if (text is null || !IsWellFormed(text))
        {
            return false;
        }

        var separator = text.LastIndexOf(Separator);
        var number = int.Parse(text.AsSpan(separator + 1), CultureInfo.InvariantCulture);
        skin = new SkinId(text[..separator], number);
        return true;
    }

    /// <summary>
    /// The base skin of <paramref name="championId"/>; null when it is no champion id, which
    /// must never reach an art URL.
    /// </summary>
    public static SkinId? BaseOf(string? championId) =>
        TryParse(championId + Separator + BaseNumber, out var skin) ? skin : null;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{ChampionId}{Separator}{Number}");

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex Syntax();
}
