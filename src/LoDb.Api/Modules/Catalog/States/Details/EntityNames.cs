using System.Globalization;
using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.States.Fields;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// The names, in the page's language, of what the changed fields of one entity designate by
/// id: the subject of a field (a spell, a rune, a skin) and the entities a value names (an item
/// of a recipe, a required champion, a skin, a rune of a row).
/// </summary>
/// <remarks>
/// The page's catalog answers first; the catalog compared to, when given, names what the
/// version after no longer holds (a removed skin, a component dropped from a recipe).
/// </remarks>
internal sealed class EntityNames
{
    private readonly IndexedEntity entity;
    private readonly CatalogSnapshot[] catalogs;

    public EntityNames(IndexedEntity entity, CatalogSnapshot catalog, CatalogSnapshot? compared)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(catalog);
        this.entity = entity;
        catalogs = compared is null ? [catalog] : [catalog, compared];
    }

    /// <summary>
    /// The name of the subject of <paramref name="key"/>; null for a key without one, for the
    /// subjects the front labels itself (stats, ratings, rows), or when no catalog names it.
    /// </summary>
    public string? SubjectOf(FieldKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Subject is not { } subject)
        {
            return null;
        }

        return ChangeFields.Of(key.Field).Subject switch
        {
            SubjectKind.SpellSlot => First(catalog => SpellNameOf(catalog, subject)),
            SubjectKind.Rune => First(catalog => RuneNameOf(catalog, subject)),
            SubjectKind.Skin => First(catalog => SkinNameOf(catalog, subject)),
            _ => null,
        };
    }

    /// <summary>
    /// The name of the entity <paramref name="value"/> designates in a field of
    /// <paramref name="key"/>; null for a value that designates none.
    /// </summary>
    public string? LabelOf(FieldKey key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return key.Field switch
        {
            ChangeField.Recipe => First(catalog => catalog.Items.Find(value)?.Name),
            ChangeField.RequiredChampion or ChangeField.RequiredAlly =>
                First(catalog => catalog.FindChampion(value)?.Summary.Name),
            ChangeField.Skins => First(catalog => SkinNameOf(catalog, value)),
            ChangeField.RuneSlot => First(catalog => RuneNameOf(catalog, value)),
            _ => null,
        };
    }

    private static string? RuneNameOf(CatalogSnapshot catalog, string id) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var rune)
            ? catalog.FindRune(rune)?.Rune.Name
            : null;

    private string? First(Func<CatalogSnapshot, string?> nameIn) =>
        catalogs.Select(nameIn).FirstOrDefault(static name => name is not null);

    // The spells come in their Q, W, E, R order, the order of the slot tokens.
    private string? SpellNameOf(CatalogSnapshot catalog, string slot)
    {
        var position = FieldTokens.SpellSlots.ToList().IndexOf(slot);
        return position < 0
            ? null
            : ChampionIn(catalog)?.Spells.ElementAtOrDefault(position)?.Name;
    }

    private string? SkinNameOf(CatalogSnapshot catalog, string id) =>
        ChampionIn(catalog) is { } champion
        && champion.Skins.FirstOrDefault(skin => skin.Id == id) is { } skin
            ? SkinName.Display(skin, champion.Summary.Name)
            : null;

    private ChampionDetail? ChampionIn(CatalogSnapshot catalog) =>
        entity.Resource == ResourceType.Champions ? catalog.FindChampion(entity.Id) : null;
}
