using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// A field the diff engine compared, as the block « Ce qui a changé » reads it.
/// </summary>
internal static class FieldChanges
{
    /// <summary>
    /// The field of <paramref name="diff"/>: a text by its segments alone, any other kind by
    /// its values, each entity among them named.
    /// </summary>
    public static FieldChange Of(FieldDiff diff, EntityNames names)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(names);
        var template = ChangeFields.Of(diff.Key.Field);
        var isText = template.Kind == FieldKind.Text;
        return new FieldChange
        {
            Field = diff.Key.Field,
            Subject = ChangedFieldList.SubjectOf(
                diff.Key,
                names.SubjectOf(diff.Key) ?? diff.SubjectName),
            Kind = template.Kind,
            ValueKind = template.Values,
            Before = isText ? [] : ValuesOf(diff.Key, diff.Before, names),
            After = isText ? [] : ValuesOf(diff.Key, diff.After, names),
            Segments = isText ? diff.Segments : [],
        };
    }

    private static ChangeValue[] ValuesOf(
        FieldKey key,
        IReadOnlyList<string> values,
        EntityNames names) =>
        [.. values.Select(value => new ChangeValue
        {
            Value = value,
            Label = names.LabelOf(key, value),
        })];
}
