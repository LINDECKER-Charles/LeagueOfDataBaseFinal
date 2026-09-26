using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>
/// A summoner spell as the list shows it, with the values its facets filter on: modes,
/// edition, level and cooldown.
/// </summary>
internal sealed record SummonerCard
{
    /// <summary>
    /// Data Dragon id, such as SummonerFlash or its Classic twin SummonerFlash_Jade.
    /// </summary>
    public required string Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    public required Edition Edition { get; init; }

    /// <summary>The same-named spell of the other game, when there is one.</summary>
    public CounterpartLink? Counterpart { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string Description { get; init; }

    /// <summary>Cooldown per rank, in seconds.</summary>
    public required IReadOnlyList<double> Cooldown { get; init; }

    /// <summary>Summoner level that unlocks the spell.</summary>
    public int? SummonerLevel { get; init; }

    public required IReadOnlyList<SummonerMode> Modes { get; init; }

    public static SummonerCard Of(SummonerSpell spell, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new SummonerCard
        {
            Id = spell.Id,
            Key = spell.Key,
            Name = spell.Name,
            CanonicalPath = catalog.PathOf(spell).Value,
            Image = images.Of(EntityImages.Icon(spell)),
            Edition = spell.Edition,
            Counterpart = spell.Counterpart is { } twin
                ? CounterpartLink.Of(twin, catalog.TwinOf(spell) is { } held
                    ? catalog.PathOf(held).Value
                    : null)
                : null,
            Description = DdragonText.Clean(spell.Description),
            Cooldown = spell.Cooldown,
            SummonerLevel = spell.SummonerLevel,
            Modes = SummonerMode.Of(spell.Modes),
        };
    }
}
