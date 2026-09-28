namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Pace and patience of the outbox worker (<c>LoDb:Outbox</c>), checked when the host
/// starts.
/// </summary>
/// <remarks>
/// With the defaults, a failing message is tried again after 30 s, then 1, 2, 4… minutes,
/// one hour at most between two attempts, and is dead after its tenth failure, about three
/// hours after it was queued.
/// </remarks>
public sealed class OutboxOptions
{
    public const string SectionName = "LoDb:Outbox";

    private const int DefaultBatchSize = 20;
    private const int DefaultPollSeconds = 5;
    private const int DefaultMaxAttempts = 10;
    private const int DefaultRetrySeconds = 30;
    private const int DefaultMaxRetryMinutes = 60;
    private const int DefaultLeaseMinutes = 5;

    /// <summary>Messages an instance takes at once.</summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>Wait between two looks at the table while it has nothing due.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(DefaultPollSeconds);

    /// <summary>Attempts at a message before it is marked <c>dead</c>.</summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>Wait after a first failure; it doubles with every failure.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(DefaultRetrySeconds);

    /// <summary>Longest wait between two attempts.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(DefaultMaxRetryMinutes);

    /// <summary>
    /// How long a taken message stays out of the other instances' reach: if its instance
    /// dies while sending, it is tried again once the lease is over.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(DefaultLeaseMinutes);
}
