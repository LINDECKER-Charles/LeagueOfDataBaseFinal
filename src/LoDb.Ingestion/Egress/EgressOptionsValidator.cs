using LoDb.Ingestion.Egress.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Refuses at startup a configuration that would fail every fetch, or that the resilience
/// pipeline would only reject on the first request.
/// </summary>
internal sealed class EgressOptionsValidator : IValidateOptions<EgressOptions>
{
    // Bounds enforced by the Polly timeout strategy.
    private static readonly TimeSpan MinAttemptTimeout = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(1);

    public ValidateOptionsResult Validate(string? name, EgressOptions options)
    {
        var failures = new List<string>();
        if (AllowList.Normalize(options.AllowedHosts).Count == 0)
        {
            failures.Add("LoDb:Egress:AllowedHosts must list at least one host.");
        }

        AddIf(failures, options.FetchConcurrency < 1, "FetchConcurrency must be positive.");
        AddIf(failures, options.MaxRetryAttempts < 1, "MaxRetryAttempts must be positive.");
        AddIf(failures, options.MaxRedirects < 0, "MaxRedirects must not be negative.");
        AddIf(failures, options.MaxResponseBytes < 1, "MaxResponseBytes must be positive.");
        AddIf(
            failures,
            options.AttemptTimeout < MinAttemptTimeout || options.AttemptTimeout > MaxDuration,
            "AttemptTimeout must lie between 10 ms and one day.");
        AddIf(
            failures,
            options.RetryBaseDelay < TimeSpan.Zero || options.RetryBaseDelay > MaxDuration,
            "RetryBaseDelay must lie between zero and one day.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
