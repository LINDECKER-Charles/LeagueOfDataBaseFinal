using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections;

namespace LoDb.Domain.States.Fingerprints;

/// <summary>
/// A projection read as the fingerprint and the diff engine both read it: one field per key,
/// the first one given. Both agreeing on it keeps "same fingerprint" and "no changed key" one
/// fact.
/// </summary>
internal static class DistinctFields
{
    /// <summary>The fields in the projection's order, each key once.</summary>
    public static IReadOnlyList<ProjectedField> InOrder(IReadOnlyList<ProjectedField> projection) =>
        [.. projection.DistinctBy(static field => field.Key)];

    /// <summary>The fields by key.</summary>
    public static Dictionary<FieldKey, ProjectedField> ByKey(
        IReadOnlyList<ProjectedField> projection) =>
        InOrder(projection).ToDictionary(static field => field.Key);

    /// <summary>
    /// The fields in canonical order: templates in the order of <see cref="ChangeField"/>, then
    /// subjects ordinal.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Canonical(
        IReadOnlyList<ProjectedField> projection) =>
        [.. InOrder(projection)
            .OrderBy(static field => field.Key.Field)
            .ThenBy(static field => field.Key.Subject, StringComparer.Ordinal)];

    /// <summary>Whether two fields of the same key hold the same values, in order.</summary>
    public static bool HaveSameValues(ProjectedField? before, ProjectedField? after) =>
        before is not null
        && after is not null
        && before.Values.SequenceEqual(after.Values, StringComparer.Ordinal);
}
