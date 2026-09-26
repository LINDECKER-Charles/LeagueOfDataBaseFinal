namespace LoDb.Infrastructure.Outbox;

/// <summary>What one batch of the outbox did.</summary>
/// <param name="Claimed">Messages taken.</param>
/// <param name="Sent">Messages the relay accepted.</param>
/// <param name="Retried">Failed messages due again later.</param>
/// <param name="Dead">Failed messages given up.</param>
/// <param name="BatchWasFull">True when more messages may be due right now.</param>
public sealed record DispatchSummary(
    int Claimed,
    int Sent,
    int Retried,
    int Dead,
    bool BatchWasFull)
{
    /// <summary>A batch that took nothing.</summary>
    public static DispatchSummary Empty { get; } = new(0, 0, 0, 0, BatchWasFull: false);
}
