namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>
/// The ingestion state of the versions (<c>ddragon_version</c>). The pipeline moves a version
/// through its states with the context; the store holds the writes N instances race on.
/// </summary>
public interface IDdragonVersionStore
{
    /// <summary>
    /// Records a version seen in <c>versions.json</c> as <c>discovered</c>
    /// (<c>INSERT … ON CONFLICT DO NOTHING</c>).
    /// </summary>
    /// <returns>True if the version was unknown; a known version keeps its state.</returns>
    Task<bool> DiscoverAsync(string version, CancellationToken cancellationToken);

    /// <summary>Every known version, by version string.</summary>
    Task<IReadOnlyList<DdragonVersion>> ListAsync(CancellationToken cancellationToken);
}
