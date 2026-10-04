using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Text;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>A rune the build editor offers, in its slot.</summary>
internal sealed record RuneOption
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string ShortDesc { get; init; }

    public static RuneOption Of(Rune rune, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(rune);
        ArgumentNullException.ThrowIfNull(images);
        return new RuneOption
        {
            Id = rune.Id,
            Key = rune.Key,
            Name = rune.Name,
            Image = images.Of(EntityImages.Icon(rune)),
            ShortDesc = DdragonText.Clean(rune.ShortDesc),
        };
    }
}
