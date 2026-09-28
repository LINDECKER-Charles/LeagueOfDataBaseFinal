using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// The only spelling of CommunityDragon's URLs, the sole source of chromas (UP 9).
/// </summary>
/// <remarks>
/// CommunityDragon cuts one directory per major.minor patch and serves its newest one under
/// <see cref="LatestPatch"/>. Asset paths are stored relative to <see cref="Root"/>, patch
/// included, and hotlinked through <see cref="Asset"/>.
/// </remarks>
public static class CommunityDragonUrls
{
    /// <summary>Alias of the newest patch, the fallback of a patch CommunityDragon lacks.</summary>
    public const string LatestPatch = "latest";

    private const string GameData = "plugins/rcp-be-lol-game-data/global/default";
    private const string GameAssetsPrefix = "/lol-game-data/assets/";
    private const char Separator = '/';
    private const char SegmentSeparator = '.';
    private const int PatchSegments = 2;

    public static Uri Root { get; } = new("https://raw.communitydragon.org/");

    /// <summary>"16.14.1" → "16.14": the directory CommunityDragon cuts for a version.</summary>
    public static string Patch(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return string.Join(
            SegmentSeparator,
            version.Value.Split(SegmentSeparator).Take(PatchSegments));
    }

    /// <summary>Every skin of the patch with its chromas, keyed by skin id.</summary>
    public static Uri Skins(string patch) => new(Root, $"{patch}/{GameData}/v1/skins.json");

    /// <summary>
    /// Path of a game asset relative to <see cref="Root"/>: CommunityDragon serves its assets
    /// in lowercase, whatever the case of the game path (UP 9).
    /// </summary>
    /// <param name="patch">The patch that answered.</param>
    /// <param name="gamePath">
    /// "/lol-game-data/assets/v1/champion-chroma-images/103/103052.png".
    /// </param>
    public static string AssetPath(string patch, string gamePath)
    {
        ArgumentNullException.ThrowIfNull(gamePath);
        var relative = gamePath
            .Replace(GameAssetsPrefix, string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant()
            .TrimStart(Separator);
        return $"{patch}/{GameData}/{relative}";
    }

    /// <summary>The hotlink URL of an <see cref="AssetPath"/>.</summary>
    public static Uri Asset(string assetPath) => new(Root, assetPath);
}
