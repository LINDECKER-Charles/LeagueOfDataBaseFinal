namespace LoDb.Infrastructure.Analytics.Rollup;

/// <summary>
/// Folds the views of <c>analytics_event</c> into <c>analytics_daily</c>. Idempotent: a day
/// folded again gets the same aggregate; an imported day is never replaced.
/// </summary>
public interface IAnalyticsRollup
{
    /// <summary>Folds today, still open: its aggregate stays a few minutes behind.</summary>
    /// <returns>The days written: today, or none when it saw no view yet.</returns>
    Task<IReadOnlyList<DateOnly>> RollupTodayAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Folds the closed days whose aggregate is missing, or was written before the day
    /// closed; the others are final.
    /// </summary>
    /// <returns>The days written, in order.</returns>
    Task<IReadOnlyList<DateOnly>> RollupClosedAsync(CancellationToken cancellationToken);
}
