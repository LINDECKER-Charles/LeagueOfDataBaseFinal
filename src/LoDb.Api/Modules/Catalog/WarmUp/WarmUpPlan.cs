using LoDb.Api.Modules.Catalog.Champions;
using LoDb.Api.Modules.Catalog.Items;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Runes;
using LoDb.Api.Modules.Catalog.Summoners;
using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Items;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// The images a warm-up fetches: every image the lists of the requested resources show,
/// over all their pages, so that any page of them lands whole.
/// </summary>
/// <remarks>
/// The same images as the list endpoints resolve: portraits, item icons with the upgrades
/// the cards link, path and rune icons, spell icons. The detail pages resolve their own
/// images before answering, and need no warm-up.
/// </remarks>
internal static class WarmUpPlan
{
    /// <summary>The images in the requested order, each manifest key once.</summary>
    public static IReadOnlyList<PlannedImage> Of(
        CatalogSnapshot catalog,
        IReadOnlyList<ResourceType> resources)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(resources);
        return
        [
            .. resources
                .SelectMany(resource => NamedImagesOf(catalog, resource)
                    .Select(named => named.Image is { } image
                        ? new PlannedImage(resource, image, named.Name)
                        : null))
                .OfType<PlannedImage>()
                .DistinctBy(static planned => (planned.Image.ManifestType, planned.Image.File)),
        ];
    }

    private static IEnumerable<(DdragonImage? Image, string Name)> NamedImagesOf(
        CatalogSnapshot catalog,
        ResourceType resource) => resource switch
        {
            ResourceType.Champions => ChampionList.EntriesOf(catalog)
                .Select(static champion =>
                    (EntityImages.Portrait(champion), champion.Summary.Name)),
            ResourceType.Items => ItemsOf(catalog)
                .Select(static item => (EntityImages.Icon(item), item.Name)),
            ResourceType.Runes => RuneList.TreesOf(catalog)
                .Select(static tree => (EntityImages.Icon(tree), tree.Name))
                .Concat(RuneList.Located(catalog).Select(static located =>
                    (EntityImages.Icon(located.Rune), located.Rune.Name))),
            ResourceType.Summoners => SummonerList.EntriesOf(catalog)
                .Select(static spell => (EntityImages.Icon(spell), spell.Name)),
            _ => [],
        };

    // The cards link what they build into, which the list resolves with them.
    private static IEnumerable<Item> ItemsOf(CatalogSnapshot catalog)
    {
        var listed = ItemList.EntriesOf(catalog);
        return listed.Concat(ItemList.RelatedOf(listed, catalog));
    }
}
