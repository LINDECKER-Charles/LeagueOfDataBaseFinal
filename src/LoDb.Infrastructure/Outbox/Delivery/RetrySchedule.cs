namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>Exponential backoff between the attempts at a message.</summary>
internal static class RetrySchedule
{
    // 2^30 times any sane first delay already exceeds every cap: no overflow past it.
    private const int MaxDoublings = 30;

    /// <summary>
    /// Wait after the failure of attempt <paramref name="attempts"/> (1 for the first):
    /// <see cref="OutboxOptions.RetryDelay"/> doubled at each failure, capped at
    /// <see cref="OutboxOptions.MaxRetryDelay"/>.
    /// </summary>
    public static TimeSpan Delay(int attempts, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var doublings = Math.Clamp(attempts - 1, 0, MaxDoublings);
        var delay = options.RetryDelay.Ticks * Math.Pow(2, doublings);
        return delay >= options.MaxRetryDelay.Ticks
            ? options.MaxRetryDelay
            : TimeSpan.FromTicks((long)delay);
    }

    /// <summary>True when the failure of attempt <paramref name="attempts"/> is the last.</summary>
    public static bool IsLast(int attempts, OutboxOptions options) =>
        attempts >= options.MaxAttempts;
}
