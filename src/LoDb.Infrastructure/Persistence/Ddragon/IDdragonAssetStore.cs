namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>The image manifest (<c>ddragon_asset</c>), safe to write from N instances.</summary>
public interface IDdragonAssetStore
{
    /// <summary>
    /// Records a batch in one statement (<c>INSERT … ON CONFLICT</c>).
    /// </summary>
    /// <param name="entries">Entries; for a key given twice, the last one wins.</param>
    /// <param name="force">
    /// False: a recorded key keeps its row, so an absence stays an absence. True (forced
    /// ingestion): the row is replaced when it differs.
    /// </param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>The number of rows inserted or replaced.</returns>
    Task<int> RecordAsync(
        IReadOnlyCollection<DdragonAssetEntry> entries,
        bool force,
        CancellationToken cancellationToken);

    /// <summary>The recorded assets of one version and type, by key.</summary>
    Task<IReadOnlyList<DdragonAsset>> ListAsync(
        string version,
        string type,
        CancellationToken cancellationToken);
}
