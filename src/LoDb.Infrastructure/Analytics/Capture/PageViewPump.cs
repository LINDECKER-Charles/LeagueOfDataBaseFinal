using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Drains <see cref="PageViewQueue"/>: each batch is resolved view by view, then written in
/// one copy. A view that cannot be resolved, or a batch that cannot be written, is dropped
/// and counted: the capture never stops on one bad view or one database outage.
/// </summary>
/// <remarks>Logs carry counts only, never a path, an address or a user agent.</remarks>
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

    private async Task<List<AnalyticsEvent>> ResolveAsync(
        IReadOnlyList<CapturedView> batch,
        CancellationToken cancellationToken)
    {
        var views = new List<AnalyticsEvent>(batch.Count);
        foreach (var captured in batch)
        {
            try
            {
                if (await factory.CreateAsync(captured, cancellationToken) is { } view)
                {
                    views.Add(view);
                }
            }
            catch (Exception exception) when (!IsStopping(exception, cancellationToken))
            {
                _metrics.RecordDropped(1, AnalyticsMetrics.Unresolved);
                LogUnresolved(logger, exception.GetType().Name);
            }
        }

        return views;
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    // The type and the state only: the details of a failed copy may quote a row, its
    // address and user agent included.
    [LoggerMessage(
        EventName = "analytics.views.write_failed",
        Level = LogLevel.Error,
        Message = "{Count} page views could not be written ({ErrorType} {SqlState}) and are lost.")]
    private static partial void LogWriteFailed(
        ILogger logger,
        int count,
        string errorType,
        string? sqlState);

    // The type only: the message of a resolution failure may quote the requested path.
    [LoggerMessage(
        EventName = "analytics.views.unresolved",
        Level = LogLevel.Warning,
        Message = "A page view could not be resolved ({ErrorType}) and is lost.")]
    private static partial void LogUnresolved(ILogger logger, string errorType);
}
