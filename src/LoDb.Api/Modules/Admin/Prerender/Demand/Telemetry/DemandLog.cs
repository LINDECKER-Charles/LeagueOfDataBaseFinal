namespace LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;

/// <summary>
/// The automatic queue's log lines: one per read of the SSR that brought pages, one per batch
/// the worker rendered, never one per page (ADR 0003, ADR 0010). Paths stay out of them: the
/// admin lists the most requested pages.
/// </summary>
internal static partial class DemandLog
{
    [LoggerMessage(
        EventName = "prerender.demand.collected",
        Level = LogLevel.Information,
        Message = "Read {Read} refused pages from the SSR: {Queued} queued, {Rejected} outside"
            + " the catalogue, {Deferred} left until their catalogue is stored.")]
    public static partial void Collected(
        ILogger logger,
        int read,
        int queued,
        int rejected,
        int deferred);

    [LoggerMessage(
        EventName = "prerender.demand.unreadable",
        Level = LogLevel.Warning,
        Message = "The SSR's refused pages are out of reach at {Url}: nothing new enters the"
            + " queue until they are read again.")]
    public static partial void Unreadable(ILogger logger, Uri url);

    [LoggerMessage(
        EventName = "prerender.demand.readable",
        Level = LogLevel.Information,
        Message = "The SSR's refused pages are read again at {Url}.")]
    public static partial void Readable(ILogger logger, Uri url);

    [LoggerMessage(
        EventName = "prerender.demand.drained",
        Level = LogLevel.Information,
        Message = "The automatic queue rendered a batch of {Pages} pages into epoch {Epoch}:"
            + " {Written} written, {Skipped} skipped, {Failed} failed, {Deferred} left for"
            + " later, {Bytes} bytes; {Refusals} refusals of the SSR waited out in {DurationMs}"
            + " ms. Last reason: {LastReason}")]
    public static partial void Drained(
        ILogger logger,
        int pages,
        int epoch,
        int written,
        int skipped,
        int failed,
        int deferred,
        long bytes,
        int refusals,
        long durationMs,
        string lastReason);

    [LoggerMessage(
        EventName = "prerender.demand.stopped",
        Level = LogLevel.Warning,
        Message = "The automatic queue stops until its next tick: {Reason}")]
    public static partial void Stopped(ILogger logger, string reason);
}
