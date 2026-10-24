using LoDb.Infrastructure.Persistence.Robots.Writing;
using LoDb.Infrastructure.Robots.Detection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>
/// Drains <see cref="RobotJournalQueue"/>: each batch turned into hits, then written in one
/// transaction. A batch that cannot be written is dropped and counted: the journal never
/// stops on a database outage.
/// </summary>
/// <remarks>
/// At most one line per failed batch, with counts only, never a path, an address or a user
/// agent.
/// </remarks>
internal sealed partial class RobotJournalPump(
    RobotJournalQueue queue,
    JournalHits hits,
    IRobotJournalWriter writer,
    RobotsMetrics metrics,
    ILogger<RobotJournalPump> logger) : IRobotJournalPump
{
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
        IReadOnlyList<JournalRecord> batch,
        CancellationToken cancellationToken)
    {
        var rows = await hits.BuildAsync(batch, cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        try
        {
            await writer.WriteAsync(rows, cancellationToken);
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            metrics.RecordDropped(rows.Count, RobotsMetrics.WriteFailed);
            LogWriteFailed(
                logger,
                rows.Count,
                exception.GetType().Name,
                (exception as PostgresException)?.SqlState);
            return 0;
        }

        var undeclared = rows.Count(static row => row.Detection == RobotDetections.Rate);
        metrics.RecordWritten(rows.Count - undeclared, RobotDetections.Agent);
        metrics.RecordWritten(undeclared, RobotDetections.Rate);
        return rows.Count;
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    // The type and the state only: the details of a failed copy may quote a row.
    [LoggerMessage(
        EventName = "robots.hits.write_failed",
        Level = LogLevel.Error,
        Message = "{Count} robot hits could not be written ({ErrorType} {SqlState}) and are lost.")]
    private static partial void LogWriteFailed(
        ILogger logger,
        int count,
        string errorType,
        string? sqlState);
}
