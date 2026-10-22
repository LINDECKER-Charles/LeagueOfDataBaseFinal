using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Testing.States;

/// <summary>
/// An index of states held in memory, for the tests of what reads it: a snapshot built with
/// <see cref="EntityStateSnapshot.Of"/>, or a database that cannot give one.
/// </summary>
public sealed class InMemoryEntityStateIndex : IEntityStateIndex
{
    /// <summary>The snapshot every read gets; empty until a test sets one.</summary>
    public EntityStateSnapshot Snapshot { get; set; } = EntityStateSnapshot.Empty;

    /// <summary>Whether a read fails as an unreachable database with no copy held does.</summary>
    public bool IsUnavailable { get; set; }

    public ValueTask<EntityStateSnapshot> GetAsync(CancellationToken cancellationToken) =>
        IsUnavailable
            ? throw new StateIndexUnavailableException(
                "The index of states is unavailable.",
                new TimeoutException("The test made the database unreachable."))
            : ValueTask.FromResult(Snapshot);
}
