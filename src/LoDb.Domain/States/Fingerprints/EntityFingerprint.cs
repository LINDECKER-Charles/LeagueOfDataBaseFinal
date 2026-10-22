using LoDb.Domain.States.Projections;

namespace LoDb.Domain.States.Fingerprints;

/// <summary>
/// The fingerprint of an en_US entry's displayed projection: two versions of an entity with
/// the same fingerprint are one state, and differ by no field the page shows.
/// </summary>
public static class EntityFingerprint
{
    /// <summary>
    /// Lowercase hexadecimal SHA-256 of a canonical serialization of
    /// <paramref name="projection"/>: fields in the order of <c>ChangeField</c>, subjects
    /// ordinal, values as projected (<see cref="FingerprintScheme.Current"/>).
    /// </summary>
    /// <remarks>
    /// Differs for two projections if and only if <c>EntityDiff.ChangedKeys</c> finds a key:
    /// a state never opens without a field to show.
    /// </remarks>
    public static string Of(IReadOnlyList<ProjectedField> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        throw new NotImplementedException("L1");
    }
}
