using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Paths;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Hotlinks;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>
/// A champion as the list shows it: its portrait, its lore teaser, and the values its facets
/// filter on: roles, resource, range, ratings and base stats.
/// </summary>
internal sealed record ChampionCard
{
    private const int BaseSkin = 0;

    /// <summary>Public id, such as MonkeyKing: the page and the art are keyed by it.</summary>
    public required string Id { get; init; }

    /// <summary>Numeric key, such as 62.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Title { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>
    /// The base skin's loading-screen portrait, hotlinked from Data Dragon (UP 8): the art of
    /// the list's card.
    /// </summary>
    public required Uri LoadingArt { get; init; }

    /// <summary>The lore teaser, template tokens removed.</summary>
    public required string Blurb { get; init; }

    /// <summary>Roles, as Data Dragon names them ("Mage").</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Language-independent resource token ("mana", "energy", "none").</summary>
    public required string Resource { get; init; }

    /// <summary>The resource as the catalog's language names it.</summary>
    public string? Partype { get; init; }

    public AttackRangeClass? AttackRange { get; init; }

    public ChampionRatings? Ratings { get; init; }

    public required IReadOnlyList<ChampionStat> Stats { get; init; }

    public static ChampionCard Of(
        ChampionDetail champion,
        CatalogSnapshot catalog,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        var summary = champion.Summary;
        return new ChampionCard
        {
            Id = summary.Id,
            Key = summary.Key,
            Name = summary.Name,
            Title = summary.Title,
            CanonicalPath = catalog.PathOf(champion).Value,
            Image = images.Of(EntityImages.Portrait(champion)),
            LoadingArt = ChampionHotlinks.Art(summary.Id, ChampionArtKind.Loading, BaseSkin),
            Blurb = DdragonText.Clean(summary.Blurb),
            Tags = summary.Tags,
            Resource = catalog.ResourceTokenOf(champion),
            Partype = summary.Partype,
            AttackRange = Domain.Derived.Ranges.AttackRange.ClassOf(summary),
            Ratings = summary.Info,
            Stats = ChampionStats.Of(summary.Stats),
        };
    }
}
