using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>Refuses at startup a pace that would stall or flood the outbox.</summary>
internal sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    private const int MaxBatchSize = 500;
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(1);

    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddIf(
            failures,
            options.BatchSize is < 1 or > MaxBatchSize,
            $"BatchSize must lie between 1 and {MaxBatchSize}.");
        AddIf(failures, options.MaxAttempts < 1, "MaxAttempts must be positive.");
        AddIf(
            failures,
            OutOfRange(options.PollInterval),
            "PollInterval must lie in 1 ms – 1 day.");
        AddIf(failures, OutOfRange(options.RetryDelay), "RetryDelay must lie in 1 ms – 1 day.");
        AddIf(failures, OutOfRange(options.Lease), "Lease must lie in 1 ms – 1 day.");
        AddIf(
            failures,
            OutOfRange(options.MaxRetryDelay) || options.MaxRetryDelay < options.RetryDelay,
            "MaxRetryDelay must lie in RetryDelay – 1 day.");
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool OutOfRange(TimeSpan value) => value < MinDelay || value > MaxDelay;

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
