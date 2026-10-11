namespace LoDb.Ingestion.Images;

/// <summary>
/// The <c>type</c> column of <c>ddragon_asset</c>: the legacy manifest names, so that a row
/// compares directly with <c>manifest/{version}/{type}.json</c> (L1.8).
/// </summary>
/// <remarks>
/// As in the legacy manifest, the key is the image file name, or the icon path for runes,
/// and the champion type also holds the passive and ability icons.
/// </remarks>
public static class ManifestTypes
{
    /// <summary>Champion portraits, passives and abilities.</summary>
    public const string Champion = "champion";

    public const string Item = "item";

    /// <summary>Rune path and rune icons, recorded per version although unversioned.</summary>
    public const string Runes = "runesReforged";

    public const string Summoner = "summoner";

    /// <summary>
    /// The WebP variants of champion art, a type of the new stack the legacy manifest never
    /// had: one row per art and width (<see cref="DdragonImage.ManifestKey"/>).
    /// </summary>
    public const string ChampionArt = "championArt";

    /// <summary>The types of the legacy manifest, art variants excluded.</summary>
    public static IReadOnlyList<string> All { get; } = [Champion, Item, Runes, Summoner];
}
