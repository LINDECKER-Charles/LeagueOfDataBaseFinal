using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Analytics;

/// <summary>Refuses at startup a capture that would never write, or a wrong retention.</summary>
internal sealed class AnalyticsOptionsValidator : IValidateOptions<AnalyticsOptions>
{
    private const int MaxBatchSize = 10_000;
    private const int MaxPartitionsAhead = 31;

    // Two runs of the daily job can be two days apart (AnalyticsOptions.ClientDataEraseDays):
    // a shorter erasure window would leave the views between them with their client data.
    private const int MinClientDataEraseDays = 2;

    // The CNIL's ceiling for the data of an audience measurement.
    private const int MaxEventRetentionMonths = 25;

    // A retention in months is at least this many days a month.
    private const int ShortestMonthDays = 28;

    private static readonly TimeSpan MinWindow = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinJobStatementTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxJobStatementTimeout = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, AnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddIf(failures, options.QueueCapacity < 1, "QueueCapacity must be positive.");
        AddIf(
            failures,
            options.BatchSize is < 1 or > MaxBatchSize,
            $"BatchSize must lie between 1 and {MaxBatchSize}.");
        AddIf(
            failures,
            options.BatchWindow < MinWindow || options.BatchWindow > MaxWindow,
            "BatchWindow must lie in 1 ms – 1 minute.");
        AddIf(
            failures,
            options.EventRetentionMonths is < 1 or > MaxEventRetentionMonths,
            $"EventRetentionMonths must lie between 1 and {MaxEventRetentionMonths}.");
        AddIf(
            failures,
            !IsWithinEventRetention(options.ClientDataRetention, options.EventRetentionMonths),
            "ClientDataRetention must be positive and shorter than the events' retention.");
        AddIf(
            failures,
            options.PartitionsAhead is < 1 or > MaxPartitionsAhead,
            $"PartitionsAhead must lie between 1 and {MaxPartitionsAhead}.");
        AddDailyBoundFailures(failures, options);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddDailyBoundFailures(List<string> failures, AnalyticsOptions options)
    {
        AddIf(
            failures,
            options.ClientDataEraseDays < MinClientDataEraseDays,
            $"ClientDataEraseDays must be at least {MinClientDataEraseDays}.");
        AddIf(failures, options.MaxVisitorsPerDay < 1, "MaxVisitorsPerDay must be positive.");
        AddIf(
            failures,
            options.MaxRefererHostsPerDay < 1,
            "MaxRefererHostsPerDay must be positive.");
        AddIf(
            failures,
            options.JobStatementTimeout < MinJobStatementTimeout
                || options.JobStatementTimeout > MaxJobStatementTimeout,
            "JobStatementTimeout must lie in 1 second – 1 hour.");
    }

    private static bool IsWithinEventRetention(TimeSpan retention, int eventMonths) =>
        retention > TimeSpan.Zero
        && retention <= TimeSpan.FromDays(eventMonths * ShortestMonthDays);

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
