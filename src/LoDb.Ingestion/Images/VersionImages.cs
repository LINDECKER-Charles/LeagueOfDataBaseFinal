using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Pipeline.Datasets;

namespace LoDb.Ingestion.Images;

/// <summary>
/// The images an entity references: what the version ingestion fetches, and what the image
/// resolver looks up for a page.
/// </summary>
/// <remarks>
/// Splash art, skins, chromas and ability videos are hotlinked, never ingested (UP 8). An
/// entry without an image (a summary standing in for a missing champion file, UP 3) yields
/// none.
/// </remarks>
public static class VersionImages
{
    /// <summary>Portrait, passive and abilities, in that order.</summary>
    public static IReadOnlyList<DdragonImage> Of(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        var images = new List<DdragonImage>();
        Add(images, DdragonImageKind.Champion, champion.Summary.Image);
        Add(images, DdragonImageKind.Passive, champion.Passive?.Image);
        foreach (var spell in champion.Spells)
        {
            Add(images, DdragonImageKind.ChampionSpell, spell.Image);
        }

        return images;
    }

    public static IReadOnlyList<DdragonImage> Of(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var images = new List<DdragonImage>();
        Add(images, DdragonImageKind.Item, item.Image);
        return images;
    }

    public static IReadOnlyList<DdragonImage> Of(SummonerSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        var images = new List<DdragonImage>();
        Add(images, DdragonImageKind.SummonerSpell, spell.Image);
        return images;
    }

    /// <summary>The path icon, then the icon of every rune of its slots.</summary>
    public static IReadOnlyList<DdragonImage> Of(RuneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var images = new List<DdragonImage>();
        Add(images, DdragonImageKind.Rune, tree.Icon);
        foreach (var rune in tree.Slots.SelectMany(static slot => slot.Runes))
        {
            Add(images, DdragonImageKind.Rune, rune.Icon);
        }

        return images;
    }

    /// <summary>
    /// Every image of the four resources, debris items included (recipes show them), each
    /// manifest key once.
    /// </summary>
    internal static List<DdragonImage> Of(VersionDatasets datasets) =>
    [
        .. datasets.Champions.Entries.SelectMany(Of)
            .Concat(datasets.Items.Entries.SelectMany(Of))
            .Concat(datasets.Runes.Entries.SelectMany(Of))
            .Concat(datasets.Summoners.Entries.SelectMany(Of))
            .DistinctBy(static image => (image.ManifestType, image.File)),
    ];

    /// <summary>
    /// The image a dataset field names, or <see langword="null"/> for a key the manifest
    /// cannot hold: not a Data Dragon image name, skipped rather than thrown, so that one odd
    /// entry never blocks a whole version.
    /// </summary>
    internal static DdragonImage? ImageOf(DdragonImageKind kind, string? file) =>
        !string.IsNullOrWhiteSpace(file) && file.Length <= DdragonImage.MaxFileLength
            ? new DdragonImage(kind, file)
            : null;

    private static void Add(List<DdragonImage> images, DdragonImageKind kind, string? file)
    {
        if (ImageOf(kind, file) is { } image)
        {
            images.Add(image);
        }
    }
}
