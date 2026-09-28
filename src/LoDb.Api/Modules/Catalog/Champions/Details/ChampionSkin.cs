using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>A skin of the champion, chroma entries left out, with its art and chromas.</summary>
internal sealed record ChampionSkin
{
    /// <summary>Data Dragon skin id, such as 103001.</summary>
    public required string Id { get; init; }

    /// <summary>Number of the skin's art, 0 for the base skin.</summary>
    public required int Number { get; init; }

    /// <summary>Display name: the base skin reads as the champion.</summary>
    public required string Name { get; init; }

    public required ChampionArt Art { get; init; }

    public required IReadOnlyList<ChampionChroma> Chromas { get; init; }

    public static ChampionSkin Of(Skin skin, ChampionSummary champion)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(champion);
        return new ChampionSkin
        {
            Id = skin.Id,
            Number = skin.Number,
            Name = SkinName.Display(skin, champion.Name),
            Art = ChampionArt.Of(champion.Id, skin.Number),
            Chromas = [.. skin.Chromas.Select(ChampionChroma.Of)],
        };
    }
}
