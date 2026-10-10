namespace LoDb.Ingestion.Queue;

/// <summary>
/// Units of on-demand work an instance may take per minute, in fixed windows of one minute;
/// zero lets none through.
/// </summary>
/// <remarks>
/// The budgets bound the volume of cold work a sweep can cause, not the requests of a single
/// visitor, who never comes near them: the crawlers' queued requests (C5) and the visitors'
/// synchronous ingestions (<see cref="OnDemandBudgets"/>).
/// </remarks>
/// <param name="perMinute">Units a window holds.</param>
/// <param name="timeProvider">The clock the windows follow.</param>
internal sealed class MinuteBudget(int perMinute, TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private DateTimeOffset windowStart;
    private int taken;

    /// <summary>Takes one unit, if the window has one left.</summary>
    public bool TryTake() => TakeUpTo(1) == 1;

    /// <summary>Takes as many of <paramref name="wanted"/> units as the window has left.</summary>
    /// <returns>The units taken, from none to <paramref name="wanted"/>.</returns>
    public int TakeUpTo(int wanted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wanted);
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (now - windowStart >= Window)
            {
                windowStart = now;
                taken = 0;
            }

            var granted = Math.Min(wanted, perMinute - taken);
            taken += granted;
            return granted;
        }
    }
}
