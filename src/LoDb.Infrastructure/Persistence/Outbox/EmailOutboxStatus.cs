namespace LoDb.Infrastructure.Persistence.Outbox;

/// <summary>
/// Delivery state of a queued e-mail, stored as <c>pending</c>, <c>sent</c> or <c>dead</c>.
/// </summary>
public enum EmailOutboxStatus
{
    /// <summary>Waiting for its first or next attempt.</summary>
    Pending,

    /// <summary>Accepted by the mail server.</summary>
    Sent,

    /// <summary>Given up after the maximum number of attempts.</summary>
    Dead,
}
