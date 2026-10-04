using System.Globalization;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;

namespace LoDb.Api.Modules.Builds.Rendering;

/// <summary>
/// The rune paths and picks of a build, each looked up where the legacy page looked it up:
/// a pick only in the path it was picked in, so a rune moved to another path is a ghost.
/// </summary>
internal static class RuneViews
{
    /// <param name="tree">The path on the patch; null when the patch lacks it.</param>
    /// <param name="id">The id the build stores.</param>
    /// <param name="images">The resolved images of the answer.</param>
    public static RunePathView Path(RuneTree? tree, int id, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(images);
        return tree is null
            ? new RunePathView
            {
                Id = id,
                Name = NameOf(id),
                Icon = CatalogImage.Absent,
                Missing = true,
            }
            : new RunePathView
            {
                Id = id,
                Key = tree.Key,
                Name = tree.Name,
                Icon = images.Of(EntityImages.Icon(tree)),
                Missing = false,
            };
    }

    /// <param name="tree">The path the rune was picked in; null when the patch lacks it.</param>
    /// <param name="id">The id the build stores.</param>
    /// <param name="images">The resolved images of the answer.</param>
    public static PerkView Perk(RuneTree? tree, int id, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var rune = tree?.Slots.SelectMany(static slot => slot.Runes)
            .FirstOrDefault(rune => rune.Id == id);
        return rune is null
            ? new PerkView
            {
                Id = id,
                Name = NameOf(id),
                Icon = CatalogImage.Absent,
                Missing = true,
            }
            : new PerkView
            {
                Id = id,
                Key = rune.Key,
                Name = rune.Name,
                Icon = images.Of(EntityImages.Icon(rune)),
                ShortDesc = rune.ShortDesc,
                Missing = false,
            };
    }

    private static string NameOf(int id) => id.ToString(CultureInfo.InvariantCulture);
}
