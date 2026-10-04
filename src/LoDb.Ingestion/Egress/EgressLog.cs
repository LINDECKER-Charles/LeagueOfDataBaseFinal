using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Summary lines of the egress, one per batch at most, never one per URL: a cold champion
/// list is hundreds of images.
/// </summary>
internal static partial class EgressLog
{
    // Error: either the caller builds URLs it must never build, or the client is being
    // probed. Both must show, and nothing else reports them.
    [LoggerMessage(
        EventName = "fetch.allowlist.refused",
        Level = LogLevel.Error,
        Message = "Refused {Refused} of {Batch} fetches outside the allow-list, hosts {Hosts}.")]
    public static partial void AllowListRefused(
        ILogger logger,
        int refused,
        int batch,
        string hosts);

    [LoggerMessage(
        EventName = "fetch.redirect.refused",
        Level = LogLevel.Error,
        Message = "Refused {Refused} of {Batch} fetches redirected outside the allow-list.")]
    public static partial void RedirectRefused(ILogger logger, int refused, int batch);

    // Warning: an upstream outage the next run retries; the volume is the signal.
    [LoggerMessage(
        EventName = "fetch.batch.degraded",
        Level = LogLevel.Warning,
        Message = "{Failed} of {Batch} fetches got no verdict.")]
    public static partial void BatchDegraded(ILogger logger, int failed, int batch);
}
