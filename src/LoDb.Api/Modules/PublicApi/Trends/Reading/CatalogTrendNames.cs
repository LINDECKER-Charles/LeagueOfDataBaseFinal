using System.Globalization;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.PublicApi.Trends.Reading;

/// <summary>
/// The names of the latest catalog in en_US, as the site shows them, keyed as go-api keyed
/// them: the rune paths and the runes by id and by key.
/// </summary>
/// <remarks>
/// Only what is stored is read: a catalog missing from the storage makes the ranking a 503,
/// where go-api left the names out.
/// </remarks>
internal sealed class CatalogTrendNames(ICatalogReader catalogs) : ITrendNames
{
    private static readonly IReadOnlyDictionary<string, string> NoNames =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<string, string>> NamesAsync(
        TrendType type,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (await catalogs.GetLatestAsync(cancellationToken) is not { } latest)
        {
            return NoNames;
        }

        var load = await catalogs.GetAsync(
            latest,
            DdragonLanguage.EnUs,
            ColdDemand.StoredOnly,
            cancellationToken);
        if (!load.IsReady)
        {
            throw new DependencyUnavailableException(
                $"The {latest} catalog in en_US is not in the storage.");
        }

        return NamesIn(load.Catalog, type);
    }

    private static Dictionary<string, string> NamesIn(CatalogSnapshot catalog, TrendType type)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (type == TrendType.Champions)
        {
            foreach (var champion in catalog.Champions.Entries)
            {
                Name(names, champion.Summary.Id, champion.Summary.Name);
            }
        }
        else if (type == TrendType.Items)
        {
            foreach (var item in catalog.Items.Entries)
            {
                Name(names, item.Id, item.Name);
            }
        }
        else if (type == TrendType.Summoners)
        {
            foreach (var spell in catalog.Summoners.Entries)
            {
                Name(names, spell.Id, spell.Name);
            }
        }
        else if (type == TrendType.Runes)
        {
            foreach (var tree in catalog.Runes.Entries)
            {
                NameRune(names, tree.Id, tree.Key, tree.Name);
                foreach (var rune in tree.Slots.SelectMany(static slot => slot.Runes))
                {
                    NameRune(names, rune.Id, rune.Key, rune.Name);
                }
            }
        }

        return names;
    }

    private static void NameRune(Dictionary<string, string> names, int id, string key, string name)
    {
        if (name.Length == 0)
        {
            return;
        }

        Name(names, key, name);
        names[id.ToString(CultureInfo.InvariantCulture)] = name;
    }

    // Last one wins, as in go-api's map.
    private static void Name(Dictionary<string, string> names, string id, string name)
    {
        if (id.Length > 0 && name.Length > 0)
        {
            names[id] = name;
        }
    }
}
