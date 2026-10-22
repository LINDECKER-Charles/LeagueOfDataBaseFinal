using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Projections;

namespace LoDb.Domain.Tests.States.Fingerprints;

/// <summary>
/// Whether two projections are one state: the fingerprint and the changed keys must say the
/// same thing, or a state would open without a field to show.
/// </summary>
internal static class StateAssert
{
    internal static void Same(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after)
    {
        Assert.Equal(EntityFingerprint.Of(before), EntityFingerprint.Of(after));
        Assert.Empty(EntityDiff.ChangedKeys(before, after));
    }

    internal static void Changed(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after,
        params string[] keys)
    {
        Assert.NotEqual(EntityFingerprint.Of(before), EntityFingerprint.Of(after));
        Assert.Equal(
            keys,
            EntityDiff.ChangedKeys(before, after).Select(static key => key.ToDotted()));
    }
}
