using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>The changed fields of a state as the API writes them.</summary>
internal static class ChangedFieldList
{
    /// <summary>
    /// The fields of <paramref name="keys"/>, their subjects named by
    /// <paramref name="names"/>, or left unnamed without a catalog (the history).
    /// </summary>
    public static IReadOnlyList<ChangedField> Of(IReadOnlyList<FieldKey> keys, EntityNames? names)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return [.. keys.Select(key => new ChangedField
        {
            Field = key.Field,
            Subject = SubjectOf(key, names?.SubjectOf(key)),
        })];
    }

    /// <summary>The subject of <paramref name="key"/>; null for a template without one.</summary>
    public static ChangeSubject? SubjectOf(FieldKey key, string? name)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Subject is { } subject && ChangeFields.Of(key.Field).Subject is { } kind
            ? new ChangeSubject { Kind = kind, Key = subject, Name = name }
            : null;
    }
}
