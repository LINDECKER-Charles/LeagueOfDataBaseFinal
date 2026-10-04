namespace LoDb.Ingestion.Pipeline.Watch;

/// <summary>
/// One pass of the patch watch (ADR 0003): records the new versions of <c>versions.json</c>
/// as <c>discovered</c>, then queues every version due for an ingestion attempt.
/// </summary>
/// <remarks>
/// The periodic job runs it under its lock, every <c>LoDb:Ingestion:WatchPeriod</c>. A
/// failure to read <c>versions.json</c> throws: nothing is recorded and the next pass tries
/// again.
/// </remarks>
public interface IPatchWatch
{
    /// <returns>The number of versions queued.</returns>
    Task<int> WatchAsync(CancellationToken cancellationToken);
}
