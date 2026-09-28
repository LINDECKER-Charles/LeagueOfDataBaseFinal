namespace LoDb.Ingestion.Queue;

/// <summary>
/// The on-demand requests waiting for the background worker of this instance.
/// </summary>
public interface IOnDemandBacklog
{
    /// <summary>Requests waiting, the one being processed excluded.</summary>
    int Count { get; }

    /// <summary>Processes the queued requests until <paramref name="cancellationToken"/>.</summary>
    Task ConsumeAsync(CancellationToken cancellationToken);
}
