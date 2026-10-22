using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Projections;

namespace LoDb.Domain.States.Diff;

/// <summary>
/// The diff engine: compares two projections of an entity and knows nothing of
/// versions. Which state is compared, and whether it is a return, is the index's.
/// </summary>
public static class EntityDiff
{
    /// <summary>
    /// The keys of the fields whose values differ between two en_US projections, in the order
    /// of the projections: what the index stores with the state <paramref name="after"/> opens.
    /// </summary>
    /// <remarks>A field present on one side only is changed.</remarks>
    public static IReadOnlyList<FieldKey> ChangedKeys(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var sides = new Sides(DistinctFields.ByKey(before), DistinctFields.ByKey(after));
        return [.. KeysOf(before, after, sides).Where(key => !DistinctFields.HaveSameValues(
            sides.Before.GetValueOrDefault(key),
            sides.After.GetValueOrDefault(key)))];
    }

    /// <summary>
    /// The fields of <paramref name="scope"/> between two projections in the page's language,
    /// in the order of <paramref name="after"/>; none for a new entity
    /// (<paramref name="before"/> null), whose state has no changed key.
    /// </summary>
    public static IReadOnlyList<FieldDiff> Compare(
        IReadOnlyList<ProjectedField>? before,
        IReadOnlyList<ProjectedField> after,
        DiffScope scope)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(scope);
        if (before is null)
        {
            return [];
        }

        var sides = new Sides(DistinctFields.ByKey(before), DistinctFields.ByKey(after));
        return [.. KeysOf(before, after, sides)
            .Where(scope.Keys.Contains)
            .Select(key => FieldOf(key, sides, scope))];
    }

    // The keys of after in its order, then those of before alone (the fields gone).
    private static IEnumerable<FieldKey> KeysOf(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after,
        Sides sides) =>
        DistinctFields.InOrder(after).Select(static field => field.Key).Concat(
            DistinctFields.InOrder(before)
                .Select(static field => field.Key)
                .Where(key => !sides.After.ContainsKey(key)));

    private static FieldDiff FieldOf(FieldKey key, Sides sides, DiffScope scope)
    {
        var before = sides.Before.GetValueOrDefault(key);
        var after = sides.After.GetValueOrDefault(key);
        var diff = new FieldDiff
        {
            Key = key,
            SubjectName = SubjectNameOf(key, sides.After) ?? SubjectNameOf(key, sides.Before),
            Before = [],
            After = [],
            Segments = [],
        };
        if (ChangeFields.Of(key.Field).Kind == FieldKind.Text)
        {
            return diff with
            {
                Segments = TextDiff.Of(TextOf(before), TextOf(after), scope.Language),
            };
        }

        // Equal in this language (a lagging translation, an en_US fallback): after alone.
        return diff with
        {
            Before = DistinctFields.HaveSameValues(before, after) ? [] : before?.Values ?? [],
            After = after?.Values ?? [],
        };
    }

    private static string TextOf(ProjectedField? field) =>
        FirstValue(field) ?? string.Empty;

    private static string? FirstValue(ProjectedField? field) =>
        field is { Values.Count: > 0 } ? field.Values[0] : null;

    // A spell, a rune or a skin is named by its own name field in the same projection.
    private static string? SubjectNameOf(
        FieldKey key,
        Dictionary<FieldKey, ProjectedField> fields)
    {
        var nameField = ChangeFields.Of(key.Field).Subject switch
        {
            SubjectKind.SpellSlot => ChangeField.SpellName,
            SubjectKind.Rune => ChangeField.RuneName,
            SubjectKind.Skin => ChangeField.SkinName,
            _ => (ChangeField?)null,
        };
        return nameField is { } field && key.Subject is { } subject
            ? FirstValue(fields.GetValueOrDefault(FieldKey.Of(field, subject)))
            : null;
    }

    private sealed record Sides(
        Dictionary<FieldKey, ProjectedField> Before,
        Dictionary<FieldKey, ProjectedField> After);
}
