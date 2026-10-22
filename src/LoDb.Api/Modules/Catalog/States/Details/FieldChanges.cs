using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// A field the diff engine compared, as the block « Ce qui a changé » reads it.
/// </summary>
internal static class FieldChanges
{
    /// <summary>
    /// The field of <paramref name="diff"/>, each entity among its values named.
    /// </summary>
    /// <remarks>
    /// The values and segments are the engine's as they are: it leaves the values of a text
    /// empty, its diff being its segments, and the segments of any other kind.
    /// </remarks>
    public static FieldChange Of(FieldDiff diff, EntityNames names)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(names);
        var template = ChangeFields.Of(diff.Key.Field);
        return new FieldChange
        {
            Field = diff.Key.Field,
            Subject = ChangedFieldList.SubjectOf(
                diff.Key,
                names.SubjectOf(diff.Key) ?? diff.SubjectName),
            Kind = template.Kind,
            ValueKind = template.Values,
            Before = ValuesOf(diff.Key, diff.Before, names),
            After = ValuesOf(diff.Key, diff.After, names),
            Segments = diff.Segments,
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
