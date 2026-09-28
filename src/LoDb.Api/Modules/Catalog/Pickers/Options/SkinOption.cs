using System.Globalization;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>A skin the profile banner picker offers, its art hotlinked (UP 8).</summary>
internal sealed record SkinOption
{
    /// <summary>The id a profile stores: champion id and skin number ("Ahri_1").</summary>
    public required string Id { get; init; }

    /// <summary>Number of the skin's art, 0 for the base skin.</summary>
    public required int Number { get; init; }

    /// <summary>Display name: the base skin reads as the champion.</summary>
    public required string Name { get; init; }

    /// <summary>The loading-screen portrait, the picker's thumbnail.</summary>
    public required Uri Image { get; init; }

    /// <summary>The centered splash, the profile's banner.</summary>
    public required Uri Banner { get; init; }

    public static SkinOption Of(Skin skin, ChampionSummary champion)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(champion);
        return new SkinOption
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"{champion.Id}_{skin.Number}"),
            Number = skin.Number,
            Name = SkinName.Display(skin, champion.Name),
            Image = ChampionHotlinks.Art(champion.Id, ChampionArtKind.Loading, skin.Number),
            Banner = ChampionHotlinks.Art(champion.Id, ChampionArtKind.Centered, skin.Number),
        };
    }
}
