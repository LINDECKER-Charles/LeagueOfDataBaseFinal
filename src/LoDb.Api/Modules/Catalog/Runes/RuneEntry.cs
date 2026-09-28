using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Text;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>A rune on its path's page, with both of its descriptions.</summary>
internal sealed record RuneEntry
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string ShortDesc { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string LongDesc { get; init; }

    public static RuneEntry Of(Rune rune, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(rune);
        ArgumentNullException.ThrowIfNull(images);
        return new RuneEntry
        {
            Id = rune.Id,
            Key = rune.Key,
            Name = rune.Name,
            Image = images.Of(EntityImages.Icon(rune)),
            ShortDesc = DdragonText.Clean(rune.ShortDesc),
            LongDesc = DdragonText.Clean(rune.LongDesc),
        };
    }
}
