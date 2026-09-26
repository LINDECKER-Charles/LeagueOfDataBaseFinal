using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Search;

/// <summary>An entry the search found, enough to show it and link to its page.</summary>
internal sealed record SearchResultHit
{
    public required ResourceType Type { get; init; }

    /// <summary>The id its detail endpoint takes.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>For an item or a summoner spell, which game it belongs to.</summary>
    public Edition? Edition { get; init; }

    public static SearchResultHit Of(SearchHit hit, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new SearchResultHit
        {
            Type = hit.Type,
            Id = hit.Id,
            Name = hit.Name,
            CanonicalPath = hit.Path.Value,
            Image = images.Of(ImageOf(hit, catalog)),
            Edition = hit.Type switch
            {
                ResourceType.Items => catalog.Items.Find(hit.Id)?.Edition,
                ResourceType.Summoners => catalog.Summoners.Find(hit.Id)?.Edition,
                _ => null,
            },
        };
    }

    /// <summary>The image the hit shows, as its list card shows it.</summary>
    public static DdragonImage? ImageOf(SearchHit hit, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(catalog);
        return hit.Type switch
        {
            ResourceType.Champions => Found(catalog.Champions.Find(hit.Id), EntityImages.Portrait),
            ResourceType.Items => Found(catalog.Items.Find(hit.Id), EntityImages.Icon),
            ResourceType.Runes => Found(catalog.Runes.Find(hit.Id), EntityImages.Icon),
            ResourceType.Summoners => Found(catalog.Summoners.Find(hit.Id), EntityImages.Icon),
            _ => null,
        };
    }

    private static DdragonImage? Found<TEntry>(TEntry? entry, Func<TEntry, DdragonImage?> image)
        where TEntry : class =>
        entry is null ? null : image(entry);
}
