using LoDb.Domain.States.Fields;
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
        throw new NotImplementedException("L1");
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
        throw new NotImplementedException("L1");
    }
}
