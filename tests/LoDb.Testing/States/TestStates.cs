using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Testing.States;

/// <summary>
/// Snapshots of the index of states written in a few lines, for the tests of what reads it:
/// a state is open or closed, its path the entity's id under its resource unless the test
/// sets one (<c>with { Path = "items/3371-molten-edge" }</c>).
/// </summary>
public static class TestStates
{
    /// <summary>When the test generation was built, and when its states closed.</summary>
    public static readonly DateTimeOffset BuiltAt = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private const string Fingerprint =
        "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>A generation over <paramref name="versions"/>, oldest first.</summary>
    public static EntityStateSnapshot Snapshot(
        IEnumerable<string> versions,
        IEnumerable<IndexedState> states,
        IEnumerable<PromotedVersion>? promoted = null) =>
        EntityStateSnapshot.Of(
            new StateIndexGeneration
            {
                Id = 1,
                Scheme = 1,
                BuiltAt = BuiltAt,
                Versions = [.. versions.Select(PatchVersion.Parse)],
            },
            [.. states],
            [.. promoted ?? []]);

    /// <summary>A state opened at <paramref name="opener"/>, still open.</summary>
    public static IndexedState Open(IndexedEntity entity, string opener)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new IndexedState
        {
            Resource = entity.Resource,
            Key = entity.Key,
            Opener = PatchVersion.Parse(opener),
            Fingerprint = Fingerprint,
            Path = $"{CanonicalPath.SegmentOf(entity.Resource)}/{entity.Id}",
            ChangedFields = [],
        };
    }

    /// <summary>A state from <paramref name="opener"/> to <paramref name="last"/>, closed.</summary>
    public static IndexedState Closed(IndexedEntity entity, string opener, string last) =>
        Open(entity, opener) with { Last = PatchVersion.Parse(last), ClosedAt = BuiltAt };
}
