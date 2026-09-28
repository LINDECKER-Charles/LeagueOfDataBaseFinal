using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Locks;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// Ingests a version under its lock: datasets first, then the images they name.
/// </summary>
/// <remarks>
/// The images are those of the <c>en_US</c> datasets, or of the first language asked for:
/// file names do not depend on the language. Only a run over every listed language moves the
/// version's state, so a partial run from the command line never promotes a version.
/// </remarks>
internal sealed class VersionIngestion(
    IDistributedLock locks,
    KnownReleases releases,
    VersionStates states,
    DatasetIngestion datasets,
    StoredDatasets stored,
    ImageIngestion images,
    IngestionMetrics metrics,
    TimeProvider timeProvider,
    ILogger<VersionIngestion> logger) : IVersionIngestion
{
    private const string LockPrefix = "ingest:";

    public async Task<VersionIngestionResult> IngestAsync(
        PatchVersion version,
        IngestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(request);
        var held = await locks.TryAcquireAsync(LockName(version), cancellationToken)
            .ConfigureAwait(false);
        if (held is null)
        {
            IngestionLog.VersionLocked(logger, version.Value);
            return Ended(version, VersionIngestionOutcome.Locked);
        }

        await using (held.ConfigureAwait(false))
        {
            var run = await PlanAsync(version, request, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                IngestionLog.VersionUnknown(logger, version.Value, Codes(request.Languages));
                return Ended(version, VersionIngestionOutcome.Unknown);
            }

            return await RunAsync(run, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Name of the lock a run holds on its version.</summary>
    internal static string LockName(PatchVersion version) => LockPrefix + version.Value;

    private static VersionIngestionResult Ended(
        PatchVersion version,
        VersionIngestionOutcome outcome) => new() { Version = version, Outcome = outcome };

    private static string Codes(IReadOnlyList<DdragonLanguage>? languages) =>
        languages is null ? "all" : string.Join(',', languages.Select(static item => item.Code));

    private async Task<Run?> PlanAsync(
        PatchVersion version,
        IngestionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await releases.IsKnownAsync(version, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var listed = await releases.GetLanguagesAsync(cancellationToken).ConfigureAwait(false);
        List<DdragonLanguage> languages = [.. (request.Languages ?? listed).Distinct()];
        if (languages.Count == 0 || !languages.All(listed.Contains))
        {
            return null;
        }

        var tracked = listed.All(languages.Contains);
        return new Run(version, languages, tracked, request.Force);
    }

    private async Task<VersionIngestionResult> RunAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        if (run.Tracked)
        {
            await states.BeginAsync(run.Version, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var written = await IngestDatasetsAsync(run, cancellationToken).ConfigureAwait(false);
            var report = await IngestImagesAsync(run, cancellationToken).ConfigureAwait(false);
            var progress = new Progress(run, written, report, started);
            return report.IsComplete
                ? await CompleteAsync(progress, cancellationToken).ConfigureAwait(false)
                : await DeferAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            IngestionLog.VersionFailed(logger, run.Version.Value, exception);
            var progress = new Progress(run, Written: 0, Report: null, started);
            return await DeferAsync(progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private async Task<int> IngestDatasetsAsync(Run run, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var written = await datasets.IngestAsync(run.Version, run.Languages, cancellationToken)
            .ConfigureAwait(false);
        metrics.RecordDuration(
            IngestionMetrics.DatasetsStage,
            timeProvider.GetElapsedTime(started));
        return written;
    }

    private async Task<ImageBatchReport> IngestImagesAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var language = run.Languages.Contains(DdragonLanguage.EnUs)
            ? DdragonLanguage.EnUs
            : run.Languages[0];
        var scope = new DatasetScope { Version = run.Version, Language = language };
        var loaded = await stored.LoadAsync(scope, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"The datasets of {run.Version.Value} ({language.Code}) are not stored.");
        var batch = new ImageBatch(run.Version, VersionImages.Of(loaded), run.Force);
        return await images.IngestAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<VersionIngestionResult> CompleteAsync(
        Progress progress,
        CancellationToken cancellationToken)
    {
        var run = progress.Run;
        var (readied, promoted) = run.Tracked
            ? await states.CompleteAsync(run.Version, cancellationToken).ConfigureAwait(false)
            : (false, false);
        if (readied)
        {
            metrics.RecordVersionReady();
        }

        var elapsed = Finish(progress);
        IngestionLog.VersionCompleted(
            logger,
            run.Version.Value,
            (long)elapsed.TotalMilliseconds,
            progress.Written,
            progress.Report?.Stored ?? 0,
            promoted);
        return Result(progress, VersionIngestionOutcome.Completed, promoted);
    }

    private async Task<VersionIngestionResult> DeferAsync(
        Progress progress,
        CancellationToken cancellationToken)
    {
        var run = progress.Run;
        if (run.Tracked)
        {
            await states.DeferAsync(run.Version, cancellationToken).ConfigureAwait(false);
        }

        Finish(progress);
        if (progress.Report is { } report)
        {
            IngestionLog.VersionIncomplete(logger, run.Version.Value, report.Failed);
        }

        return Result(progress, VersionIngestionOutcome.Incomplete, promoted: false);
    }

    private TimeSpan Finish(Progress progress)
    {
        var elapsed = timeProvider.GetElapsedTime(progress.Started);
        metrics.RecordDuration(IngestionMetrics.VersionStage, elapsed);
        return elapsed;
    }

    private static VersionIngestionResult Result(
        Progress progress,
        VersionIngestionOutcome outcome,
        bool promoted) => new()
        {
            Version = progress.Run.Version,
            Outcome = outcome,
            DatasetsWritten = progress.Written,
            Images = progress.Report,
            Promoted = promoted,
        };

    private sealed record Run(
        PatchVersion Version,
        IReadOnlyList<DdragonLanguage> Languages,
        bool Tracked,
        bool Force);

    private sealed record Progress(
        Run Run,
        int Written,
        ImageBatchReport? Report,
        long Started);
}
