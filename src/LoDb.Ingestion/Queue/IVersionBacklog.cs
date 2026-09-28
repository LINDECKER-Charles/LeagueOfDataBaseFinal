using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The versions waiting for their ingestion on this instance, ingested one at a time by a
/// background worker.
/// </summary>
public interface IVersionBacklog
{
    /// <summary>Versions waiting or being ingested.</summary>
    int Count { get; }

    /// <summary>Queues a version, unless it is already waiting or being ingested.</summary>
    /// <returns>
    /// False when the backlog is full: the version stays due and the next watch queues it.
    /// </returns>
    bool TryEnqueue(PatchVersion version);

    /// <summary>Ingests the queued versions until <paramref name="cancellationToken"/>.</summary>
    Task ConsumeAsync(CancellationToken cancellationToken);
}
