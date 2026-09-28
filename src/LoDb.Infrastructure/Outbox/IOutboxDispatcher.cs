namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Sends the due messages of <c>email_outbox</c>, one batch per call; the API's outbox
/// worker calls it in a loop.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>False when no relay is configured: nothing is taken, nothing is sent.</summary>
    bool IsSendingEnabled { get; }

    /// <summary>
    /// Takes up to a batch of due messages, sends each and records its outcome. A failure
    /// of the database is logged and measured, never thrown.
    /// </summary>
    Task<DispatchSummary> DispatchAsync(CancellationToken cancellationToken);
}
