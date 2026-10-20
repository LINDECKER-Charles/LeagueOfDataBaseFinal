namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// The bill's log lines: one per reading, never one per request nor per line (ADR 0003,
/// ADR 0010). None holds the key.
/// </summary>
internal static partial class ScalewayBillingLog
{
    [LoggerMessage(
        EventName = "admin.scaleway_bill.read",
        Level = LogLevel.Information,
        Message = "Read the Scaleway bill of {Periods} months: {Lines} lines in {DurationMs} ms.")]
    public static partial void Read(ILogger logger, int periods, int lines, long durationMs);

    [LoggerMessage(
        EventName = "admin.scaleway_bill.unavailable",
        Level = LogLevel.Warning,
        Message = "The Scaleway bill could not be read after {DurationMs} ms: the panels show the"
            + " last good reading, if any, and the next attempt waits 5 minutes.")]
    public static partial void Unavailable(ILogger logger, long durationMs, Exception exception);
}
