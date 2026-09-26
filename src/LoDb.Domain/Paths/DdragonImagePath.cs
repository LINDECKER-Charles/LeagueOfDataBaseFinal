using System.Collections.Frozen;
using System.Globalization;
using LoDb.Domain.Versions;

namespace LoDb.Domain.Paths;

/// <summary>
/// Paths of Data Dragon images, relative to the CDN root (<c>…/cdn/</c>).
/// </summary>
/// <remarks>
/// The root stays with the callers, who configure and allow-list it. Dataset icons live under
/// their version; rune icons (UP 5) and champion art (UP 8) do not, and the art is hotlinked
/// rather than ingested.
/// </remarks>
public static class DdragonImagePath
{
    private const string ImageRoot = "img";
    private const string ChampionFolder = "champion";
    private const string PassiveFolder = "passive";
    private const string SpellFolder = "spell";
    private const string ItemFolder = "item";
    private const string ArtExtension = ".jpg";

    // Riot's internal spelling where it differs from the public id: the CDN serves the
    // pre-rework Fiddlesticks art under the public id, and 403s its newer skins and the whole
    // centered family, while the internal spelling answers every kind and skin (UP 7).
    private static readonly FrozenDictionary<string, string> ArtIds =
        new Dictionary<string, string>
        {
            ["Fiddlesticks"] = "FiddleSticks",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<ChampionArtKind, string> ArtFolders =
        new Dictionary<ChampionArtKind, string>
        {
            [ChampionArtKind.Splash] = "splash",
            [ChampionArtKind.Loading] = "loading",
            [ChampionArtKind.Centered] = "centered",
        }.ToFrozenDictionary();

    /// <summary>Square champion portrait ("Ahri.png").</summary>
    public static string Champion(PatchVersion version, string file) =>
        Versioned(version, ChampionFolder, file);

    public static string Passive(PatchVersion version, string file) =>
        Versioned(version, PassiveFolder, file);

    /// <summary>Icon of a champion ability or of a summoner spell.</summary>
    public static string Spell(PatchVersion version, string file) =>
        Versioned(version, SpellFolder, file);

    public static string Item(PatchVersion version, string file) =>
        Versioned(version, ItemFolder, file);

    /// <summary>
    /// Rune icon as <c>runesReforged.json</c> gives it, kept verbatim: the dead <c>.dds</c>
    /// icons of 7.22 to 8.7 answer 403 and are recorded as absent, never rewritten (UP 5).
    /// </summary>
    public static string RuneIcon(string icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return $"{ImageRoot}/{icon}";
    }

    /// <param name="championId">Public champion id ("Fiddlesticks").</param>
    /// <param name="kind">Art family.</param>
    /// <param name="skinNumber">Number of the skin's art files (0 for the base skin).</param>
    public static string ChampionArt(string championId, ChampionArtKind kind, int skinNumber)
    {
        ArgumentNullException.ThrowIfNull(championId);
        var artId = ArtIds.GetValueOrDefault(championId, championId);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ImageRoot}/{ChampionFolder}/{ArtFolders[kind]}/{artId}_{skinNumber}{ArtExtension}");
    }

    private static string Versioned(PatchVersion version, string folder, string file)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(file);
        return $"{version.Value}/{ImageRoot}/{folder}/{file}";
    }
}
