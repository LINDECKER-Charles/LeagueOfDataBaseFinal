using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// The only spelling of Data Dragon's URLs, aligned with the egress allow-list.
/// </summary>
/// <remarks>
/// Image paths come from <c>DdragonImagePath</c> in the domain, relative to
/// <see cref="CdnRoot"/>: <see cref="Image"/> turns them into URLs.
/// </remarks>
public static class DdragonUrls
{
    private const string DatasetExtension = ".json";

    public static Uri Root { get; } = new("https://ddragon.leagueoflegends.com/");

    /// <summary>Root of the versioned datasets and of every image.</summary>
    public static Uri CdnRoot { get; } = new(Root, "cdn/");

    /// <summary>Every version, newest first, <c>lolpatch_*</c> entries included (UP 4).</summary>
    public static Uri Versions { get; } = new(Root, "api/versions.json");

    /// <summary>Every language of Data Dragon; a version may lack some of them (UP 2).</summary>
    public static Uri Languages { get; } = new(CdnRoot, "languages.json");

    /// <param name="version">The version.</param>
    /// <param name="language">The language.</param>
    /// <param name="file">File name without extension ("championFull", "item"…).</param>
    public static Uri Dataset(PatchVersion version, DdragonLanguage language, string file)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(language);
        return new Uri(CdnRoot, $"{version.Value}/data/{language.Code}/{file}{DatasetExtension}");
    }

    /// <summary>
    /// The per-champion file, read only when the version has no <c>championFull.json</c>.
    /// </summary>
    public static Uri ChampionDetail(
        PatchVersion version,
        DdragonLanguage language,
        string championId) =>
        Dataset(version, language, $"champion/{Uri.EscapeDataString(championId)}");

    /// <summary>An image path of <c>DdragonImagePath</c>, as a URL.</summary>
    public static Uri Image(string relativePath) => new(CdnRoot, relativePath);
}
