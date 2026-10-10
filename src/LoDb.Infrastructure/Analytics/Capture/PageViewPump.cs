using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Drains <see cref="PageViewQueue"/>: each batch is resolved view by view, then written in
/// one copy. A view that cannot be resolved, or a batch that cannot be written, is dropped
/// and counted: the capture never stops on one bad view or one database outage.
/// </summary>
/// <remarks>
/// At most one line per batch and per failure, with counts only, never a path, an address or
/// a user agent.
/// </remarks>
internal sealed partial class PageViewPump(
    PageViewQueue queue,
    ViewEventFactory factory,
    PageViewBatchWriter writer,
    ILogger<PageViewPump> logger) : IPageViewPump
{
    private readonly AnalyticsMetrics _metrics = queue.Metrics;

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken) =>
        queue.WaitAsync(cancellationToken);

    public async Task<int> FlushAsync(CancellationToken cancellationToken)
    {
        var written = 0;
        for (var batch = queue.TakeBatch(); batch.Count > 0; batch = queue.TakeBatch())
        {
            written += await WriteAsync(batch, cancellationToken);
        }

        return written;
    }

    private async Task<int> WriteAsync(
        IReadOnlyList<CapturedView> batch,
        CancellationToken cancellationToken)
    {
        var views = await ResolveAsync(batch, cancellationToken);
        if (views.Count == 0)
        {
            return 0;
        }

        try
        {
            await writer.WriteAsync(views, cancellationToken);
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            _metrics.RecordDropped(views.Count, AnalyticsMetrics.WriteFailed);
            LogWriteFailed(
                logger,
                views.Count,
                exception.GetType().Name,
                (exception as PostgresException)?.SqlState);
            return 0;
        }

        _metrics.RecordWritten(views.Count);
        return views.Count;
    }

    // One line for the whole batch, never one per view: an outage behind the resolution (the
    // catalog, the database) fails every view of every batch while it lasts.
    private async Task<List<AnalyticsEvent>> ResolveAsync(
        IReadOnlyList<CapturedView> batch,
        CancellationToken cancellationToken)
    {
        var views = new List<AnalyticsEvent>(batch.Count);
        var unresolved = 0;
        string? firstError = null;
        foreach (var captured in batch)
        {
            var (view, error) = await ResolveOneAsync(captured, cancellationToken);
            if (view is not null)
            {
                views.Add(view);
            }

            unresolved += error is null ? 0 : 1;
            firstError ??= error;
        }

        if (firstError is not null)
        {
            _metrics.RecordDropped(unresolved, AnalyticsMetrics.Unresolved);
            LogUnresolved(logger, unresolved, firstError);
        }

        return views;
    }

    /// <returns>The view, null when it is not of a counted page; or the failure's type.</returns>
    private async Task<(AnalyticsEvent? View, string? Error)> ResolveOneAsync(
        CapturedView captured,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await factory.CreateAsync(captured, cancellationToken), null);
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            return (null, exception.GetType().Name);
        }
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    // The type and the state only: the details of a failed copy may quote a row, its path
    // and its visitor hash included.
    [LoggerMessage(
        EventName = "analytics.views.write_failed",
        Level = LogLevel.Error,
        Message = "{Count} page views could not be written ({ErrorType} {SqlState}) and are lost.")]
    private static partial void LogWriteFailed(
        ILogger logger,
        int count,
        string errorType,
        string? sqlState);

    // The type of the first failure only: its message may quote the requested path.
    [LoggerMessage(
        EventName = "analytics.views.unresolved",
        Level = LogLevel.Warning,
        Message = "{Count} page views of a batch could not be resolved ({ErrorType} first) "
            + "and are lost.")]
    private static partial void LogUnresolved(ILogger logger, int count, string errorType);
}
