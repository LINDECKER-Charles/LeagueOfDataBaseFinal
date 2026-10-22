using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// What the state of a page changed when it opened, as the block « Ce qui a changé dans ce
/// patch » shows it.
/// </summary>
/// <remarks>
/// The block lists exactly the fields the index marks changed, in its order, en_US keys for
/// the values of the page's language: the block, the history and the page of the patch list
/// the same fields, a field that neither side has in that language included.
/// </remarks>
internal static class StateChanges
{
    /// <summary>
    /// The changes of a state compared to nothing: a new entity, or a state whose previous one
    /// the index lacks.
    /// </summary>
    public static EntityChanges Uncompared(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new EntityChanges { Kind = state.Kind, ComparedVersion = null, Fields = [] };
    }

    /// <summary>
    /// What <paramref name="state"/> changed against <paramref name="compared"/>, the catalog
    /// of the last version of the entity's previous state in the page's language.
    /// </summary>
    public static EntityChanges Of(
        IndexedState state,
        DetailEntity entity,
        CatalogSnapshot compared)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(compared);
        var page = entity.Catalog;
        var scope = new DiffScope
        {
            Language = page.Language,
            Keys = state.ChangedFields.ToHashSet(),
        };
        var after = entity.Project(page) ?? [];
        var before = ComparedProjection.Of(entity, compared, after);
        var diffs = EntityDiff.Compare(before, after, scope).ToDictionary(static diff => diff.Key);
        var names = new EntityNames(entity.Entity, page, compared);
        return new EntityChanges
        {
            Kind = state.Kind,
            ComparedVersion = compared.Version.Value,
            Fields = [.. state.ChangedFields.Select(key => FieldOf(key, diffs, names))],
        };
    }

    private static FieldChange FieldOf(
        FieldKey key,
        Dictionary<FieldKey, FieldDiff> diffs,
        EntityNames names) =>
        diffs.TryGetValue(key, out var diff)
            ? FieldChanges.Of(diff, names)
            : FieldChanges.WithoutValues(key, names);
}
