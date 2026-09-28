using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Writes a batch with one binary copy; when a day of the batch has no partition yet (the
/// retention job has not run since the day began), creates it and copies again, once.
/// </summary>
internal sealed class PageViewBatchWriter(
    IAnalyticsEventWriter writer,
    IAnalyticsPartitions partitions)
{
    public async Task WriteAsync(
        IReadOnlyCollection<AnalyticsEvent> views,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteAsync(views, cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.CheckViolation)
        {
            var days = views
                .Select(static view => DateOnly.FromDateTime(view.OccurredAt.UtcDateTime))
                .ToList();
            await partitions.CreateAsync(days.Min(), days.Max(), cancellationToken);
            await writer.WriteAsync(views, cancellationToken);
        }
    }
}
