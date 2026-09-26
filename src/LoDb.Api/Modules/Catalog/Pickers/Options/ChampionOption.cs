using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>A champion the favorite and build pickers offer.</summary>
internal sealed record ChampionOption
{
    public required string Id { get; init; }

    /// <summary>Numeric id the game uses ("103").</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    public static ChampionOption Of(ChampionDetail champion, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(images);
        return new ChampionOption
        {
            Id = champion.Summary.Id,
            Key = champion.Summary.Key,
            Name = champion.Summary.Name,
            Image = images.Of(EntityImages.Portrait(champion)),
        };
    }
}
