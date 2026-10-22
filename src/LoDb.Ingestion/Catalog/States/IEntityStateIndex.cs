using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Catalog.States;

/// <summary>
/// The index of entity states as one process reads it: an immutable snapshot of the
/// current generation of the compiled fingerprint scheme, which every reader queries in memory.
/// </summary>
/// <remarks>
/// The single rule of canonicals, the sitemaps, IndexNow, the patch pages and the re-render of
/// closed states all read it: never Postgres per request.
/// </remarks>
public interface IEntityStateIndex
{
    /// <summary>
    /// The snapshot to read; <see cref="EntityStateSnapshot.Empty"/> while no generation of
    /// the scheme is current (nothing built yet).
    /// </summary>
    /// <exception cref="StateIndexUnavailableException">
    /// No snapshot is held and the database cannot give one: a transient failure, which the
    /// API answers with a 503 and <c>Retry-After</c>.
    /// </exception>
    ValueTask<EntityStateSnapshot> GetAsync(CancellationToken cancellationToken);
}
