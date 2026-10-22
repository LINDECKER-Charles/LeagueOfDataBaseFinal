using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>
/// An entity a patch changed, as the catalog that shows it names it: the version's for a
/// state opened there, the version before's for an entity retired there.
/// </summary>
internal sealed record PatchEntity
{
    private const char PathSeparator = '/';

    public required IndexedState State { get; init; }

    public required PatchChangeKind Kind { get; init; }

    /// <summary>Its id in the catalog, with the case of that version.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public DdragonImage? Image { get; init; }

    /// <summary>
    /// The entity of <paramref name="state"/> in <paramref name="catalog"/>; named by its id
    /// should the catalog lack it, which the index then contradicts.
    /// </summary>
    public static PatchEntity Of(
        IndexedState state,
        PatchChangeKind kind,
        CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        var id = IdOf(state);
        var found = Find(catalog, state.Resource, id);
        return new PatchEntity
        {
            State = state,
            Kind = kind,
            Id = found?.Id ?? id,
            Name = found?.Name ?? id,
            Image = found?.Image,
        };
    }

    /// <summary>
    /// Its entry on the page: the fields its state changed, named in
    /// <paramref name="catalog"/>, none for a retirement.
    /// </summary>
    public PatchChangeEntry ToEntry(ImageSet images, CatalogSnapshot catalog, CanonicalLink link)
    {
        ArgumentNullException.ThrowIfNull(images);
        var names = new EntityNames(new IndexedEntity(State.Resource, Id), catalog, null);
        return new PatchChangeEntry
        {
            Id = Id,
            Name = Name,
            Image = images.Of(Image),
            Kind = Kind,
            ChangedFields = Kind == PatchChangeKind.Retired
                ? []
                : ChangedFieldList.Of(State.ChangedFields, names),
            Link = link,
        };
    }

    // The stored path keeps the id of the opener version: its case, and no slug to strip.
    private static string IdOf(IndexedState state)
    {
        var start = state.Path.IndexOf(PathSeparator, StringComparison.Ordinal) + 1;
        var segment = state.Path[start..];
        return CanonicalPath.IdOf(state.Resource, segment) ?? state.Key;
    }

    private static Found? Find(CatalogSnapshot catalog, ResourceType resource, string id) =>
        resource switch
        {
            ResourceType.Champions => catalog.FindChampion(id) is { } champion
                ? new Found(
                    champion.Summary.Id,
                    champion.Summary.Name,
                    EntityImages.Portrait(champion))
                : null,
            ResourceType.Items => catalog.Items.Find(id) is { } item
                ? new Found(item.Id, item.Name, EntityImages.Icon(item))
                : null,
            ResourceType.Runes => catalog.Runes.Find(id) is { } tree
                ? new Found(id, tree.Name, EntityImages.Icon(tree))
                : null,
            ResourceType.Summoners => FindSummoner(catalog, id) is { } spell
                ? new Found(spell.Id, spell.Name, EntityImages.Icon(spell))
                : null,
            _ => null,
        };

    // A spell's id is matched as the index keys it, whatever its case in that version.
    private static SummonerSpell? FindSummoner(
        CatalogSnapshot catalog,
        string id) =>
        catalog.Summoners.Find(id)
        ?? catalog.Summoners.Entries.FirstOrDefault(spell =>
            string.Equals(spell.Id, id, StringComparison.OrdinalIgnoreCase));

    private sealed record Found(string Id, string Name, DdragonImage? Image);
}
