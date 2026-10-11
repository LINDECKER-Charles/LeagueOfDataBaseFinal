using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Paths;

namespace LoDb.Ingestion.Images.Art;

/// <summary>
/// The WebP variants of a champion's base-skin art stored as blobs, so that a page's
/// largest image comes from the site, sized for the screen, rather than from Data Dragon at
/// full size: the centered splash of the page's hero, and the loading-screen portrait of the
/// list's card.
/// </summary>
/// <remarks>
/// The art is hotlinked as well (UP 8): a client falls back on it while a version has no
/// variant. Data Dragon serves one art per champion whatever the version; the manifest
/// records it per version, like the rune icons, and the blobs, addressed by content, are
/// stored once.
/// </remarks>
public static class ArtVariants
{
    /// <summary>The loading-screen portrait's own width, narrower than any variant.</summary>
    public const int LoadingWidth = 308;

    private const int BaseSkin = 0;

    /// <summary>The widths of the centered splash, 1280 pixels wide at full size.</summary>
    public static IReadOnlyList<int> CenteredWidths { get; } = [480, 960, 1280];

    /// <summary>The centered splash at each of <see cref="CenteredWidths"/>.</summary>
    public static IReadOnlyList<DdragonImage> CenteredOf(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        var path = DdragonImagePath.ChampionArt(
            champion.Summary.Id,
            ChampionArtKind.Centered,
            BaseSkin);
        return
        [
            .. CenteredWidths.Select(width =>
                DdragonImage.ArtVariant(DdragonImageKind.CenteredArt, path, width)),
        ];
    }

    /// <summary>The loading-screen portrait at its own width: a change of format only.</summary>
    public static IReadOnlyList<DdragonImage> LoadingOf(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        var path = DdragonImagePath.ChampionArt(
            champion.Summary.Id,
            ChampionArtKind.Loading,
            BaseSkin);
        return [DdragonImage.ArtVariant(DdragonImageKind.LoadingArt, path, LoadingWidth)];
    }

    /// <summary>Every variant of the champion, those of one art next to each other.</summary>
    public static IReadOnlyList<DdragonImage> Of(ChampionDetail champion) =>
        [.. CenteredOf(champion), .. LoadingOf(champion)];
}
