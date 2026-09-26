using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Derived.Runes;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>
/// A rune as the list shows it, with the values its facets filter on: its path and its row.
/// </summary>
internal sealed record RuneCard
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string ShortDesc { get; init; }

    /// <summary>Key of its path, such as Precision.</summary>
    public required string Tree { get; init; }

    /// <summary>Its row: "keystone", then "row1" to "row3".</summary>
    public required string Slot { get; init; }

    /// <summary>The page of its path: runes have none of their own.</summary>
    public required string CanonicalPath { get; init; }

    public static RuneCard Of(RuneLocation location, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        var rune = location.Rune;
        return new RuneCard
        {
            Id = rune.Id,
            Key = rune.Key,
            Name = rune.Name,
            Image = images.Of(EntityImages.Icon(rune)),
            ShortDesc = DdragonText.Clean(rune.ShortDesc),
            Tree = location.Tree.Key,
            Slot = RuneSlotToken.Of(location.SlotIndex),
            CanonicalPath = catalog.PathOf(location.Tree).Value,
        };
    }
}
