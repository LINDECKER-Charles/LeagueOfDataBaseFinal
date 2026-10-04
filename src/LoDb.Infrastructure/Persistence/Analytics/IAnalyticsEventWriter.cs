namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>
/// Appends captured views to <c>analytics_event</c> in one binary copy, the batch of the
/// analytics queue.
/// </summary>
/// <remarks>
/// All or nothing: a view whose day has no partition fails the whole batch. Text longer than
/// its column is cut to the size the entity declares, so that no value fails it.
/// <see cref="AnalyticsEvent.Id"/> is ignored: the database numbers the rows.
/// </remarks>
public interface IAnalyticsEventWriter
{
    Task WriteAsync(
        IReadOnlyCollection<AnalyticsEvent> events,
        CancellationToken cancellationToken);
}
