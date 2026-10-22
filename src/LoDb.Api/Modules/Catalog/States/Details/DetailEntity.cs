using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.States.Projections;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// The entity of a detail as its state reads it: its path, the page's catalog, and its
/// displayed projection in any catalog, the one compared to included.
/// </summary>
internal sealed record DetailEntity
{
    public required CanonicalPath Path { get; init; }

    /// <summary>The catalog of the page: its version, in its language.</summary>
    public required CatalogSnapshot Catalog { get; init; }

    /// <summary>
    /// The projection of the entity's entry in a catalog; null when the catalog lacks it. A
    /// champion is found whatever the case of its id there (FiddleSticks, then Fiddlesticks).
    /// </summary>
    public required Func<CatalogSnapshot, IReadOnlyList<ProjectedField>?> Project { get; init; }

    /// <summary>The entity as the index holds it.</summary>
    public IndexedEntity Entity => new(Path.Type, Path.Id);

    /// <summary>
    /// The language the entity's resource is written in, in <paramref name="catalog"/>: the
    /// catalog's own, or en_US where Data Dragon did not ship it yet at that version.
    /// </summary>
    public DdragonLanguage? ContentLanguageIn(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Path.Type switch
        {
            ResourceType.Champions => catalog.Champions.ContentLanguage,
            ResourceType.Items => catalog.Items.ContentLanguage,
            ResourceType.Runes => catalog.Runes.ContentLanguage,
            _ => catalog.Summoners.ContentLanguage,
        };
    }

    public static DetailEntity Of(ChampionDetail champion, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        var id = champion.Summary.Id;
        return new DetailEntity
        {
            Path = catalog.PathOf(champion),
            Catalog = catalog,
            Project = other => other.FindChampion(id) is { } entry
                ? ChampionProjection.Of(entry)
                : null,
        };
    }

    public static DetailEntity Of(Item item, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        var id = item.Id;
        return new DetailEntity
        {
            Path = catalog.PathOf(item),
            Catalog = catalog,
            Project = other => other.Items.Find(id) is { } entry ? ItemProjection.Of(entry) : null,
        };
    }

    public static DetailEntity Of(RuneTree tree, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(catalog);
        var path = catalog.PathOf(tree);
        return new DetailEntity
        {
            Path = path,
            Catalog = catalog,
            Project = other => other.Runes.Find(path.Id) is { } entry
                ? RuneTreeProjection.Of(entry)
                : null,
        };
    }

    public static DetailEntity Of(SummonerSpell spell, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(catalog);
        var id = spell.Id;
        return new DetailEntity
        {
            Path = catalog.PathOf(spell),
            Catalog = catalog,
            Project = other => other.Summoners.Find(id) is { } entry
                ? SummonerProjection.Of(entry)
                : null,
        };
    }
}
