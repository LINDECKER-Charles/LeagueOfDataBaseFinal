using System.Diagnostics;
using System.Threading.Channels;

namespace LoDb.Api.Modules.PublicApi.Metering;

/// <summary>
/// Counts the billed requests of <c>/v1</c> in memory and adds them to <c>api_usage</c> in
/// groups: a response never waits for the database.
/// </summary>
/// <remarks>
/// <para>
/// A request leaves an event in a buffer of <see cref="Capacity"/>, which a consumer folds
/// as they come into one counter per key and day. When the buffer is full the event is
/// lost, counted, and one warning per flush says how many: the metering undercounts rather
/// than slow the API down, as go-api's does.
/// </para>
/// <para>
/// A failed write keeps its counts for the next one; only the last flush, at shutdown, may
/// lose them when the database is gone by then.
/// </para>
/// </remarks>
internal sealed partial class UsageMeter : IDisposable
{
    /// <summary>Events the buffer holds between two passes, as go-api's channel.</summary>
    public const int Capacity = 4096;

    // A write never holds the counts longer than this, however slow the database.
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);

    private readonly Channel<UsageEvent> _events = Channel.CreateBounded<UsageEvent>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait });

    private readonly SemaphoreSlim _flushing = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<UsageEvent, long> _pending = [];
    private readonly UsageWriter _writer;
    private readonly UsageCalendar _calendar;
    private readonly PublicApiMetrics _metrics;
    private readonly ILogger<UsageMeter> _logger;
    private long _pendingRequests;
    private long _dropped;

    public UsageMeter(
        UsageWriter writer,
        UsageCalendar calendar,
        PublicApiMetrics metrics,
        ILogger<UsageMeter> logger)
    {
        _writer = writer;
        _calendar = calendar;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>Counts one billed request of the key, today; never waits.</summary>
    public void Record(int keyId)
    {
        if (!_events.Writer.TryWrite(new UsageEvent(keyId, _calendar.Today)))
        {
            Interlocked.Increment(ref _dropped);
            _metrics.RecordDropped();
        }
    }

    /// <summary>Folds the events into the counters as they come, until cancelled.</summary>
    public async Task CollectAsync(CancellationToken cancellationToken)
    {
        while (await _events.Reader.WaitToReadAsync(cancellationToken))
        {
            Collect();
        }
    }

    /// <summary>
    /// Adds every request counted so far to <c>api_usage</c>; a failure keeps them for the
    /// next flush.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _flushing.WaitAsync(cancellationToken);
        try
        {
            Collect();
            ReportDropped();
            KeyValuePair<UsageEvent, long>[] batch;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                batch = [.. _pending];
                _pending.Clear();
            }

            await WriteAsync(batch, cancellationToken);
        }
        finally
        {
            _flushing.Release();
        }
    }

    public void Dispose() => _flushing.Dispose();

    private async Task WriteAsync(
        KeyValuePair<UsageEvent, long>[] batch,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WriteTimeout);
        try
        {
            await _writer.WriteAsync(batch, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Restore(batch);
            throw;
        }
        catch (Exception exception)
        {
            Restore(batch);
            _metrics.RecordFlushFailure();
            LogFlushFailed(_logger, exception, batch.Length);
            return;
        }

        var requests = batch.Sum(static entry => entry.Value);
        _metrics.RecordFlush(Stopwatch.GetElapsedTime(started));
        _metrics.RecordPending(Interlocked.Add(ref _pendingRequests, -requests));
        LogFlushed(_logger, requests, batch.Length);
    }

    // Bounded, so that a flush never waits on a consumer racing a flood of requests.
    private void Collect()
    {
        lock (_gate)
        {
            var collected = 0;
            while (collected < Capacity && _events.Reader.TryRead(out var usage))
            {
                _pending[usage] = _pending.GetValueOrDefault(usage) + 1;
                collected++;
            }

            if (collected > 0)
            {
                _metrics.RecordPending(Interlocked.Add(ref _pendingRequests, collected));
            }
        }
    }

    private void Restore(KeyValuePair<UsageEvent, long>[] batch)
    {
        lock (_gate)
        {
            foreach (var (usage, count) in batch)
            {
                _pending[usage] = _pending.GetValueOrDefault(usage) + count;
            }
        }
    }

    private void ReportDropped()
    {
        var dropped = Interlocked.Exchange(ref _dropped, 0);
        if (dropped > 0)
        {
            LogDropped(_logger, dropped);
        }
    }

    [LoggerMessage(
        EventName = "publicapi.usage.flushed",
        Level = LogLevel.Debug,
        Message = "{Requests} billed requests of {Rows} key-days added to api_usage.")]
    private static partial void LogFlushed(ILogger logger, long requests, int rows);

    [LoggerMessage(
        EventName = "publicapi.usage.flush_failed",
        Level = LogLevel.Error,
        Message = "api_usage not updated for {Rows} key-days: kept for the next flush.")]
    private static partial void LogFlushFailed(ILogger logger, Exception exception, int rows);

    [LoggerMessage(
        EventName = "publicapi.usage.dropped",
        Level = LogLevel.Warning,
        Message = "{Dropped} billed requests left uncounted: the metering buffer was full.")]
    private static partial void LogDropped(ILogger logger, long dropped);
}
