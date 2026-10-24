using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots;

/// <summary>Refuses at startup a journal that would never write, or bounds out of reason.</summary>
internal sealed class RobotsOptionsValidator : IValidateOptions<RobotsOptions>
{
    private const int MaxBatchSize = 10_000;
    private const int MaxParallelism = 64;

    private static readonly TimeSpan MinWindow = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxDnsTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinJobStatementTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxJobStatementTimeout = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, RobotsOptions options)
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
        AddIf(failures, options.HitRetention <= TimeSpan.Zero, "HitRetention must be positive.");
        AddUndeclaredFailures(failures, options);
        AddDnsFailures(failures, options);
        AddIf(
            failures,
            options.JobStatementTimeout < MinJobStatementTimeout
                || options.JobStatementTimeout > MaxJobStatementTimeout,
            "JobStatementTimeout must lie in 1 s – 1 h.");
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddUndeclaredFailures(List<string> failures, RobotsOptions options)
    {
        AddIf(failures, options.UndeclaredThreshold < 1, "UndeclaredThreshold must be positive.");
        AddIf(
            failures,
            options.UndeclaredWindow <= TimeSpan.Zero,
            "UndeclaredWindow must be positive.");
        AddIf(
            failures,
            options.UndeclaredMemory < options.UndeclaredWindow,
            "UndeclaredMemory must be at least UndeclaredWindow.");
        AddIf(failures, options.MaxTrackedClients < 1, "MaxTrackedClients must be positive.");
    }

    private static void AddDnsFailures(List<string> failures, RobotsOptions options)
    {
        AddIf(
            failures,
            options.DnsTimeout <= TimeSpan.Zero || options.DnsTimeout > MaxDnsTimeout,
            "DnsTimeout must lie in 0 – 30 s, 0 excluded.");
        AddIf(failures, options.DnsLookupsPerBatch < 0, "DnsLookupsPerBatch cannot be negative.");
        AddIf(
            failures,
            options.DnsParallelism is < 1 or > MaxParallelism,
            $"DnsParallelism must lie between 1 and {MaxParallelism}.");
        AddIf(failures, options.DnsCacheSize < 1, "DnsCacheSize must be positive.");
        AddIf(
            failures,
            options.DnsCacheLifetime <= TimeSpan.Zero
                || options.DnsFailureLifetime <= TimeSpan.Zero,
            "DnsCacheLifetime and DnsFailureLifetime must be positive.");
    }

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
