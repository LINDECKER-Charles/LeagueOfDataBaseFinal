namespace LoDb.Api.Modules.Admin.Prerender.Telemetry;

/// <summary>
/// The pass's log lines: one summary per unit, never one per page, and one per change of the
/// pass the worker makes (ADR 0003, ADR 0010).
/// </summary>
internal static partial class PrerenderLog
{
    [LoggerMessage(
        EventName = "prerender.unit.completed",
        Level = LogLevel.Information,
        Message = "Pre-rendered {Version} in {Locale}: {Written} pages written, {Skipped} skipped,"
            + " {Failed} failed, {Bytes} bytes; {Refusals} refusals of the SSR waited out in"
            + " {DurationMs} ms of this run. Last failure: {LastFailure}")]
    public static partial void UnitCompleted(
        ILogger logger,
        string version,
        string locale,
        int written,
        int skipped,
        int failed,
        long bytes,
        int refusals,
        long durationMs,
        string lastFailure);

    [LoggerMessage(
        EventName = "prerender.unit.unknown",
        Level = LogLevel.Warning,
        Message = "Data Dragon lists no {Version}: its unit in {Locale} fails without a page.")]
    public static partial void UnitUnknown(ILogger logger, string version, string locale);

    [LoggerMessage(
        EventName = "prerender.unit.waiting",
        Level = LogLevel.Warning,
        Message = "The units of {Version} in pass {PassId} wait, the others go on: {Reason}")]
    public static partial void UnitWaiting(
        ILogger logger,
        string version,
        long passId,
        string reason);

    [LoggerMessage(
        EventName = "prerender.unit.abandoned",
        Level = LogLevel.Warning,
        Message = "The units of {Version} in pass {PassId} fail after {Minutes} minutes of"
            + " waiting: {Reason}")]
    public static partial void UnitAbandoned(
        ILogger logger,
        string version,
        long passId,
        int minutes,
        string reason);

    [LoggerMessage(
        EventName = "prerender.ssr.unreachable",
        Level = LogLevel.Warning,
        Message = "The SSR at {SsrBaseUrl} is out of reach: the pass waits for it, page after"
            + " page, without counting them.")]
    public static partial void SsrUnreachable(ILogger logger, string ssrBaseUrl);

    [LoggerMessage(
        EventName = "prerender.ssr.reachable",
        Level = LogLevel.Information,
        Message = "The SSR at {SsrBaseUrl} answers again after {OutageSeconds} s out of reach.")]
    public static partial void SsrReachable(ILogger logger, string ssrBaseUrl, long outageSeconds);

    [LoggerMessage(
        EventName = "prerender.pass.paused",
        Level = LogLevel.Error,
        Message = "Pass {PassId} is paused: {Reason} Every page would fail alike; resume it"
            + " once the setting is fixed.")]
    public static partial void PassPaused(ILogger logger, long passId, string reason);

    [LoggerMessage(
        EventName = "prerender.pass.completed",
        Level = LogLevel.Information,
        Message = "Pass {PassId} wrote every unit of epoch {Epoch}.")]
    public static partial void PassCompleted(ILogger logger, long passId, int epoch);

    [LoggerMessage(
        EventName = "prerender.promotion.enqueued",
        Level = LogLevel.Information,
        Message = "{Units} units of {Version}, which left the latest rank, joined pass {PassId}.")]
    public static partial void UnitsEnqueued(
        ILogger logger,
        int units,
        string version,
        long passId);

    [LoggerMessage(
        EventName = "prerender.promotion.deferred",
        Level = LogLevel.Information,
        Message = "{Version} left the latest rank while pass {PassId}, restricted to one type of"
            + " page, is active: its units join the next pass the reconciliation opens.")]
    public static partial void PromotionDeferred(ILogger logger, string version, long passId);
}
