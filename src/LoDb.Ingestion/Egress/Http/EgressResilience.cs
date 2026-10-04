using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// Resilience pipeline of the <c>ddragon</c> client: exponential retry, circuit breaker per
/// authority, timeout per attempt (innermost, so each retry gets a fresh one).
/// </summary>
internal static class EgressResilience
{
    public const string PipelineName = "egress";

    private const int ServerErrorFloor = 500;
    private const double BreakFailureRatio = 0.5;
    private const int BreakMinimumThroughput = 20;
    private static readonly TimeSpan BreakSamplingDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(15);

    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        EgressOptions options)
    {
        builder
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.RetryBaseDelay,

                // A Retry-After of an hour would stall the whole ingestion wave.
                ShouldRetryAfterHeader = false,
                ShouldHandle = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = BreakFailureRatio,
                MinimumThroughput = BreakMinimumThroughput,
                SamplingDuration = BreakSamplingDuration,
                BreakDuration = BreakDuration,
                ShouldHandle = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
            })
            .AddTimeout(options.AttemptTimeout);
    }

    /// <summary>
    /// 5xx, 408, attempt timeout or transport failure. A 403/404 is an answer, not a failure,
    /// and a cancellation by the caller is never retried.
    /// </summary>
    public static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        outcome switch
        {
            { Exception: HttpRequestException or TimeoutRejectedException } => true,
            { Result: { } response } =>
                response.StatusCode == HttpStatusCode.RequestTimeout
                || (int)response.StatusCode >= ServerErrorFloor,
            _ => false,
        };
}
