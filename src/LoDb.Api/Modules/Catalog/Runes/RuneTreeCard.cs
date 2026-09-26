using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>A rune path: a facet of the rune list, and a page of its own.</summary>
internal sealed record RuneTreeCard
{
    /// <summary>Path id, such as 8000.</summary>
    public required int Id { get; init; }

    /// <summary>Language-independent key, such as Precision: the list filters on it.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    public static RuneTreeCard Of(RuneTree tree, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new RuneTreeCard
        {
            Id = tree.Id,
            Key = tree.Key,
            Name = tree.Name,
            CanonicalPath = catalog.PathOf(tree).Value,
            Image = images.Of(EntityImages.Icon(tree)),
        };
    }
}
