using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Summoners;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>A summoner spell the build editor offers.</summary>
internal sealed record SummonerOption
{
    public required string Id { get; init; }

    /// <summary>Numeric id the game uses ("4").</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    public static SummonerOption Of(SummonerSpell spell, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(images);
        return new SummonerOption
        {
            Id = spell.Id,
            Key = spell.Key,
            Name = spell.Name,
            Image = images.Of(EntityImages.Icon(spell)),
        };
    }
}
