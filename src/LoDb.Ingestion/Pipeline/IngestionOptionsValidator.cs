using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Refuses at startup a configuration that would stall or flood the ingestion.
/// </summary>
internal sealed class IngestionOptionsValidator : IValidateOptions<IngestionOptions>
{
    // PeriodicTimer needs at least a millisecond; a day bounds every wait of the pipeline.
    private static readonly TimeSpan MinPeriod = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(1);

    // A request waits for its slot: beyond a minute, every proxy in front has given up.
    private static readonly TimeSpan MaxSlotWait = TimeSpan.FromMinutes(1);

    public ValidateOptionsResult Validate(string? name, IngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddPipelineFailures(failures, options);
        AddOnDemandFailures(failures, options);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddPipelineFailures(List<string> failures, IngestionOptions options)
    {
        AddIf(
            failures,
            OutOfRange(options.WatchPeriod),
            "WatchPeriod must lie between 1 ms and one day.");
        AddIf(
            failures,
            OutOfRange(options.RetryDelay),
            "RetryDelay must lie between 1 ms and one day.");
        AddIf(failures, options.MaxAttempts < 1, "MaxAttempts must be positive.");
        AddIf(failures, options.LanguageConcurrency < 1, "LanguageConcurrency must be positive.");
        AddIf(failures, options.RecordBatchSize < 1, "RecordBatchSize must be positive.");
    }

    private static void AddOnDemandFailures(List<string> failures, IngestionOptions options)
    {
        AddIf(failures, options.SyncConcurrency < 1, "SyncConcurrency must be positive.");
        AddIf(failures, options.QueueCapacity < 1, "QueueCapacity must be positive.");
        AddIf(
            failures,
            options.CrawlerRequestsPerMinute < 0,
            "CrawlerRequestsPerMinute must not be negative.");
        AddIf(
            failures,
            options.ColdDatasetsPerMinute < 0,
            "ColdDatasetsPerMinute must not be negative.");
        AddIf(
            failures,
            options.ColdImagesPerMinute < 0,
            "ColdImagesPerMinute must not be negative.");
        AddIf(
            failures,
            options.MaxWaitingIngestions < 0,
            "MaxWaitingIngestions must not be negative.");
        AddIf(
            failures,
            options.SlotWait < MinPeriod || options.SlotWait > MaxSlotWait,
            "SlotWait must lie between 1 ms and one minute.");
    }

    private static bool OutOfRange(TimeSpan value) => value < MinPeriod || value > MaxDuration;

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
