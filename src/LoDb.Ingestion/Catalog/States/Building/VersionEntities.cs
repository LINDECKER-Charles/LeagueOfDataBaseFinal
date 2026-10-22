using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Domain.States;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// The entities of a version, by resource, projected from its en_US catalog: those with a page
/// (<see cref="ListedEntities"/>), each key once.
/// </summary>
/// <remarks>
/// A resource whose dataset is definitively absent there (stored empty, without a content
/// language: the runes before 7.22.1) is left out: it neither extends nor interrupts its
/// states. Two ids that fold to one key keep the first, as a catalog keeps the first entry.
/// </remarks>
internal sealed class VersionEntities
{
    private readonly Dictionary<ResourceType, Dictionary<string, ProjectedEntity>> present;

    private VersionEntities(
        PatchVersion version,
        Dictionary<ResourceType, Dictionary<string, ProjectedEntity>> present)
    {
        Version = version;
        this.present = present;
    }

    public PatchVersion Version { get; }

    /// <summary>The resources whose dataset the version has, in the order of the enum.</summary>
    public IReadOnlyList<ResourceType> Resources => [.. present.Keys.Order()];

    /// <summary>
    /// Whether no resource has a dataset: a version in definitive absence, which the index
    /// skips as if Data Dragon did not list it.
    /// </summary>
    public bool IsEmpty => present.Count == 0;

    public static VersionEntities Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var present = new Dictionary<ResourceType, Dictionary<string, ProjectedEntity>>();
        if (catalog.Champions.ContentLanguage is not null)
        {
            present[ResourceType.Champions] = ByKey(ListedEntities.Champions(catalog).Select(
                champion => Entity(catalog.PathOf(champion), ChampionProjection.Of(champion))));
        }

        if (catalog.Items.ContentLanguage is not null)
        {
            present[ResourceType.Items] = ByKey(ListedEntities.Items(catalog)
                .Select(item => Entity(catalog.PathOf(item), ItemProjection.Of(item))));
        }

        if (catalog.Runes.ContentLanguage is not null)
        {
            present[ResourceType.Runes] = ByKey(ListedEntities.RuneTrees(catalog)
                .Select(tree => Entity(catalog.PathOf(tree), RuneTreeProjection.Of(tree))));
        }

        if (catalog.Summoners.ContentLanguage is not null)
        {
            present[ResourceType.Summoners] = ByKey(ListedEntities.Summoners(catalog)
                .Select(spell => Entity(catalog.PathOf(spell), SummonerProjection.Of(spell))));
        }

        return new VersionEntities(catalog.Version, present);
    }

    /// <summary>The entities of a resource the version has, by key.</summary>
    public IReadOnlyDictionary<string, ProjectedEntity> Of(ResourceType resource) =>
        present[resource];

    /// <summary>The entity of a key; null when the version has no page for it.</summary>
    public ProjectedEntity? Find(ResourceType resource, string key) =>
        present.TryGetValue(resource, out var entities) ? entities.GetValueOrDefault(key) : null;

    private static Dictionary<string, ProjectedEntity> ByKey(IEnumerable<ProjectedEntity> entities)
    {
        var byKey = new Dictionary<string, ProjectedEntity>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            byKey.TryAdd(entity.Key, entity);
        }

        return byKey;
    }

    private static ProjectedEntity Entity(
        CanonicalPath path,
        IReadOnlyList<ProjectedField> fields) => new()
        {
            Resource = path.Type,
            Key = EntityKey.Of(path.Id),
            Path = path.Value,
            Projection = fields,
            Fingerprint = EntityFingerprint.Of(fields),
        };
}
