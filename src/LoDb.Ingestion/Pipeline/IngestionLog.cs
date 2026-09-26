using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Summary lines of the ingestion: one per version, per image batch or per discovery, never
/// one per file.
/// </summary>
internal static partial class IngestionLog
{
    /// <summary>
    /// A failed version run: warning for an upstream outage, which the next attempt retries;
    /// error for anything else, which is ours to fix.
    /// </summary>
    public static void VersionFailed(ILogger logger, string version, Exception exception)
    {
        var level = LevelOf(exception);
        VersionFailed(logger, level, version, exception);
    }

    /// <summary>A failed on-demand ingestion, at the level of a failed version run.</summary>
    public static void OnDemandFailed(ILogger logger, string version, Exception exception)
    {
        var level = LevelOf(exception);
        OnDemandFailed(logger, level, version, exception);
    }

    private static LogLevel LevelOf(Exception exception) =>
        exception is EgressException ? LogLevel.Warning : LogLevel.Error;

    [LoggerMessage(
        EventName = "ingest.version.discovered",
        Level = LogLevel.Information,
        Message = "Discovered version {Version}.")]
    public static partial void VersionDiscovered(ILogger logger, string version);

    [LoggerMessage(
        EventName = "ingest.version.completed",
        Level = LogLevel.Information,
        Message = "Ingested version {Version} in {DurationMs} ms: {Datasets} datasets and"
            + " {Images} images written, promoted {Promoted}.")]
    public static partial void VersionCompleted(
        ILogger logger,
        string version,
        long durationMs,
        int datasets,
        int images,
        bool promoted);

    [LoggerMessage(
        EventName = "ingest.version.incomplete",
        Level = LogLevel.Warning,
        Message = "Version {Version} left {Failed} images without a verdict; it will be retried.")]
    public static partial void VersionIncomplete(ILogger logger, string version, int failed);

    [LoggerMessage(
        EventName = "ingest.version.failed",
        Message = "Ingestion of version {Version} failed.")]
    private static partial void VersionFailed(
        ILogger logger,
        LogLevel level,
        string version,
        Exception exception);

    [LoggerMessage(
        EventName = "ingest.version.abandoned",
        Level = LogLevel.Warning,
        Message = "Version {Version} failed {Attempts} attempts and is marked failed.")]
    public static partial void VersionAbandoned(ILogger logger, string version, int attempts);

    [LoggerMessage(
        EventName = "ingest.version.locked",
        Level = LogLevel.Information,
        Message = "Version {Version} is being ingested by another run.")]
    public static partial void VersionLocked(ILogger logger, string version);

    [LoggerMessage(
        EventName = "ingest.version.unknown",
        Level = LogLevel.Warning,
        Message = "Data Dragon does not list version {Version} or one of the languages"
            + " {Languages}.")]
    public static partial void VersionUnknown(ILogger logger, string version, string languages);

    [LoggerMessage(
        EventName = "ingest.images.completed",
        Level = LogLevel.Information,
        Message = "Settled {Settled} images of {Version} in {DurationMs} ms: {Stored} stored,"
            + " {Absent} absent, {Blobs} new blobs, {Webp} new WebP.")]
    public static partial void ImagesCompleted(
        ILogger logger,
        string version,
        long durationMs,
        int settled,
        int stored,
        int absent,
        int blobs,
        int webp);

    [LoggerMessage(
        EventName = "ingest.images.incomplete",
        Level = LogLevel.Warning,
        Message = "Settled {Settled} images of {Version}, {Failed} left without a verdict.")]
    public static partial void ImagesIncomplete(
        ILogger logger,
        string version,
        int settled,
        int failed);

    [LoggerMessage(
        EventName = "ingest.on_demand.failed",
        Message = "On-demand ingestion for version {Version} failed.")]
    private static partial void OnDemandFailed(
        ILogger logger,
        LogLevel level,
        string version,
        Exception exception);
}
