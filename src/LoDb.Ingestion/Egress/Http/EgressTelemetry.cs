using Microsoft.Extensions.DependencyInjection;
using Polly.Telemetry;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// Silences the per-request resilience lines of the <c>ddragon</c> pipeline.
/// </summary>
/// <remarks>
/// Polly logs every attempt, even a first one that succeeds, at Information: a cold
/// ingestion would write one line per image. Handled attempts (the retries) keep their
/// Warning, since their volume is the outage signal. Other pipelines are left untouched.
/// </remarks>
internal static class EgressTelemetry
{
    private const string ExecutionAttempt = "ExecutionAttempt";

    public static void Quiet(IServiceCollection services, string pipelineName) =>
        services.Configure<TelemetryOptions>(options =>
        {
            var previous = options.SeverityProvider;
            options.SeverityProvider = args =>
                IsRoutineAttempt(args, pipelineName)
                    ? ResilienceEventSeverity.None
                    : previous?.Invoke(args) ?? args.Event.Severity;
        });

    private static bool IsRoutineAttempt(SeverityProviderArguments args, string pipelineName) =>
        string.Equals(args.Source.PipelineName, pipelineName, StringComparison.Ordinal)
        && string.Equals(args.Event.EventName, ExecutionAttempt, StringComparison.Ordinal)
        && args.Event.Severity < ResilienceEventSeverity.Warning;
}
