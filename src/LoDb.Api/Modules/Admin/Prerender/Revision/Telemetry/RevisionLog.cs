namespace LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;

/// <summary>
/// The log lines of the queue of the closed states: one per run of its producer that moved,
/// one per batch its drain rendered, never one per page (ADR 0003, ADR 0010).
/// </summary>
internal static partial class RevisionLog
{
    [LoggerMessage(
        EventName = "prerender.revision.enqueued",
        Level = LogLevel.Information,
        Message = "Enqueued {Rows} rows to render again: {Pages} pages of {States} states closed"
            + " since {Since:O} (generation {GenerationId}), in epochs {Epochs}, due from"
            + " {DueAt:O}.")]
    public static partial void Enqueued(
        ILogger logger,
        int rows,
        int pages,
        int states,
        DateTimeOffset since,
        long generationId,
        string epochs,
        DateTimeOffset dueAt);

    [LoggerMessage(
        EventName = "prerender.revision.generation",
        Level = LogLevel.Information,
        Message = "The producer reads generation {GenerationId} of the index of states from now"
            + " on: its closures up to {ClosedThrough:O} are its history, rendered by the passes,"
            + " never enqueued.")]
    public static partial void NewGeneration(
        ILogger logger,
        long generationId,
        DateTimeOffset closedThrough);

    [LoggerMessage(
        EventName = "prerender.revision.index_behind",
        Level = LogLevel.Warning,
        Message = "The index of states does not cover {Version} yet: the states its promotion"
            + " closed are enqueued once it does, by the reconciliation.")]
    public static partial void IndexBehind(ILogger logger, string version);

    [LoggerMessage(
        EventName = "prerender.revision.drained",
        Level = LogLevel.Information,
        Message = "The queue of the closed states rendered a batch of {Pages} pages: {Written}"
            + " written, {Skipped} skipped, {Failed} failed, {Deferred} left for later, {Bytes}"
            + " bytes; {Refusals} refusals of the SSR waited out in {DurationMs} ms. Last"
            + " reason: {LastReason}")]
    public static partial void Drained(
        ILogger logger,
        int pages,
        int written,
        int skipped,
        int failed,
        int deferred,
        long bytes,
        int refusals,
        long durationMs,
        string lastReason);

    [LoggerMessage(
        EventName = "prerender.revision.stopped",
        Level = LogLevel.Warning,
        Message = "The queue of the closed states stops until its next tick: {Reason}")]
    public static partial void Stopped(ILogger logger, string reason);
}
