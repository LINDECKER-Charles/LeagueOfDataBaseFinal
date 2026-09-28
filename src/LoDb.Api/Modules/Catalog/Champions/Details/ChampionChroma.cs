using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>A chroma of a skin, its swatch hotlinked from CommunityDragon (UP 9).</summary>
internal sealed record ChampionChroma
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The colour family the swatch reads as ("Ruby"), "Chroma" when unknown.</summary>
    public required string Label { get; init; }

    /// <summary>Hex colours, such as #D33528.</summary>
    public required IReadOnlyList<string> Colors { get; init; }

    public required Uri Swatch { get; init; }

    public static ChampionChroma Of(Chroma chroma)
    {
        ArgumentNullException.ThrowIfNull(chroma);
        return new ChampionChroma
        {
            Id = chroma.Id,
            Name = chroma.Name,
            Label = ChromaLabel.Of(chroma),
            Colors = chroma.Colors,
            Swatch = ChampionHotlinks.ChromaSwatch(chroma),
        };
    }
}
