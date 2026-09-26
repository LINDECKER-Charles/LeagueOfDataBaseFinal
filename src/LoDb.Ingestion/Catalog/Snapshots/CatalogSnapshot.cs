using System.Collections.Frozen;
using System.Globalization;
using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Datasets;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// The catalog of one (version, language): the four resources indexed by id, their twins,
/// paths and search, immutable once built.
/// </summary>
/// <remarks>
/// Built from the stored datasets, whose items and summoner spells already carry their twin
/// (UP 6) and whose names are already cleaned (UP 10). Slugs and resource tokens are read
/// from the en_US datasets of the same version, so a path or a filter means the same thing
/// in every language (UP 13).
/// </remarks>
public sealed class CatalogSnapshot
{
    private static readonly FrozenSet<ResourceType> AllTypes =
        Enum.GetValues<ResourceType>().ToFrozenSet();

    private readonly FrozenDictionary<int, RuneLocation> runes;
    private readonly FrozenDictionary<string, string> englishItemNames;
    private readonly FrozenDictionary<string, string> englishTreeNames;
    private readonly FrozenDictionary<string, string> resourceTokens;
    private readonly CatalogSearch search;

    /// <param name="version">The version.</param>
    /// <param name="language">The language.</param>
    /// <param name="datasets">The four datasets of the (version, language).</param>
    /// <param name="english">
    /// The en_US catalog of the version; <see langword="null"/> when building it.
    /// </param>
    internal CatalogSnapshot(
        PatchVersion version,
        DdragonLanguage language,
        VersionDatasets datasets,
        CatalogSnapshot? english)
    {
        if (english is not null && english.Version != version)
        {
            throw new ArgumentException(
                "The en_US catalog is of another version.",
                nameof(english));
        }

        Version = version;
        Language = language;
        Champions = new(datasets.Champions, static champion => champion.Summary.Id);
        Items = new(datasets.Items, static item => item.Id);
        Runes = new(datasets.Runes, static tree => KeyOf(tree.Id));
        Summoners = new(datasets.Summoners, static spell => spell.Id);
        runes = LocateRunes(Runes.Entries);
        var source = english ?? this;
        englishItemNames = NamesOf(source.Items.Entries, static item => (item.Id, item.Name));
        ListedItems = [.. Items.Entries.Where(IsListed)];
        englishTreeNames = NamesOf(
            source.Runes.Entries,
            static tree => (KeyOf(tree.Id), tree.Name));
        resourceTokens = TokensOf(Champions.Entries, source.Champions);
        search = new CatalogSearch(this);
    }

    public PatchVersion Version { get; }

    public DdragonLanguage Language { get; }

    public CatalogResource<ChampionDetail> Champions { get; }

    /// <summary>Every item, debris included: recipes read them (UP 10).</summary>
    public CatalogResource<Item> Items { get; }

    /// <summary>
    /// The items of the encyclopedia, in the upstream order: debris left out, classic items
    /// kept.
    /// </summary>
    public IReadOnlyList<Item> ListedItems { get; }

    /// <summary>The rune paths, by path id ("8000"); empty before 7.22.1 (UP 1).</summary>
    public CatalogResource<RuneTree> Runes { get; }

    public CatalogResource<SummonerSpell> Summoners { get; }

    /// <summary>
    /// Whether the item belongs to the encyclopedia: not debris, its placeholder marker read
    /// from its en_US name, which Data Dragon translates in some languages (UP 10).
    /// </summary>
    public bool IsListed(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return !ItemDebris.IsDebris(item, englishItemNames.GetValueOrDefault(item.Id));
    }

    /// <summary>A rune of any path, by rune id.</summary>
    public RuneLocation? FindRune(int id) => runes.GetValueOrDefault(id);

    /// <summary>The same-named item of the other game, when this catalog carries it.</summary>
    public Item? TwinOf(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Counterpart is { } twin ? Items.Find(twin.Id) : null;
    }

    /// <summary>The same-named spell of the other game, when this catalog carries it.</summary>
    public SummonerSpell? TwinOf(SummonerSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.Counterpart is { } twin ? Summoners.Find(twin.Id) : null;
    }

    public CanonicalPath PathOf(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        return PathOf(ResourceType.Champions, champion.Summary.Id);
    }

    /// <summary>The item's page, its slug read from its en_US name.</summary>
    public CanonicalPath PathOf(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return PathOf(ResourceType.Items, item.Id);
    }

    /// <summary>The rune path's page, its slug read from its en_US name.</summary>
    public CanonicalPath PathOf(RuneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return PathOf(ResourceType.Runes, KeyOf(tree.Id));
    }

    public CanonicalPath PathOf(SummonerSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return PathOf(ResourceType.Summoners, spell.Id);
    }

    /// <summary>
    /// The language-independent resource of a champion ("mana", "energy", "none"), read from
    /// its en_US entry.
    /// </summary>
    public string ResourceTokenOf(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        return resourceTokens.GetValueOrDefault(champion.Summary.Id)
            ?? ResourceToken.Of(null, champion.Summary);
    }

    /// <summary>
    /// The entries whose name, or id, holds the query, resource by resource in the order of
    /// <see cref="ResourceType"/>, each in the upstream order.
    /// </summary>
    /// <param name="query">The search.</param>
    /// <param name="types">The resources searched; empty for all of them.</param>
    /// <param name="limit">Most hits returned per resource.</param>
    public IReadOnlyList<SearchHit> Search(
        SearchQuery query,
        IReadOnlyCollection<ResourceType> types,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        IReadOnlySet<ResourceType> searched = types.Count == 0 ? AllTypes : types.ToHashSet();
        return [.. search.Find(query, searched, limit).Select(candidate => new SearchHit
        {
            Type = candidate.Type,
            Id = candidate.Id,
            Name = candidate.Name,
            Path = PathOf(candidate.Type, candidate.Id),
        })];
    }

    private static string KeyOf(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static FrozenDictionary<int, RuneLocation> LocateRunes(IReadOnlyList<RuneTree> trees)
    {
        var located = new Dictionary<int, RuneLocation>();
        foreach (var tree in trees)
        {
            for (var index = 0; index < tree.Slots.Count; index++)
            {
                foreach (var rune in tree.Slots[index].Runes)
                {
                    var location = new RuneLocation { Tree = tree, SlotIndex = index, Rune = rune };
                    located.TryAdd(rune.Id, location);
                }
            }
        }

        return located.ToFrozenDictionary();
    }

    // The first entry of an id wins, as in the id-keyed dataset map.
    private static FrozenDictionary<string, string> NamesOf<TEntry>(
        IEnumerable<TEntry> entries,
        Func<TEntry, (string Id, string Name)> describe) =>
        entries.Select(describe)
            .DistinctBy(static entry => entry.Id)
            .ToFrozenDictionary(
                static entry => entry.Id,
                static entry => entry.Name,
                StringComparer.Ordinal);

    private static FrozenDictionary<string, string> TokensOf(
        IEnumerable<ChampionDetail> champions,
        CatalogResource<ChampionDetail> english) =>
        champions
            .DistinctBy(static champion => champion.Summary.Id)
            .ToFrozenDictionary(
                static champion => champion.Summary.Id,
                champion => ResourceToken.Of(
                    english.Find(champion.Summary.Id)?.Summary,
                    champion.Summary),
                StringComparer.Ordinal);

    private CanonicalPath PathOf(ResourceType type, string id) => type switch
    {
        ResourceType.Champions => CanonicalPath.Champion(id),
        ResourceType.Items => CanonicalPath.Item(id, englishItemNames.GetValueOrDefault(id)),
        ResourceType.Runes => CanonicalPath.RuneTree(
            int.Parse(id, CultureInfo.InvariantCulture),
            englishTreeNames.GetValueOrDefault(id)),
        ResourceType.Summoners => CanonicalPath.Summoner(id),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
