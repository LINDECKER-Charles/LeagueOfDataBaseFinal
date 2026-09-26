namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Moves the queued views into <c>analytics_event</c>, batch by batch; the writer worker
/// runs it, and a test may flush the queue itself.
/// </summary>
public interface IPageViewPump
{
    /// <summary>Waits until a view is queued.</summary>
    /// <returns>False once the queue will never hold a view again.</returns>
    ValueTask<bool> WaitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves and writes every view queued now, in binary copies of the batch size. A
    /// batch that cannot be written is dropped and counted, never thrown.
    /// </summary>
    /// <returns>The number of views written.</returns>
    Task<int> FlushAsync(CancellationToken cancellationToken);
}
