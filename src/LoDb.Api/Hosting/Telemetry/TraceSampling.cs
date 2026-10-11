using OpenTelemetry.Trace;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// The sampler of the API's traces: a share of them, decided from the trace id.
/// </summary>
/// <remarks>
/// <para>
/// A trace that reaches the API from outside is sampled by its id as well, whatever the
/// sampled flag of its <c>traceparent</c>: nginx starts the trace of every request it proxies
/// and the renderer passes it on, neither records anything, and a client must not be able to
/// make its own requests recorded. Spans started inside the API follow their parent's
/// decision, so a trace is recorded whole or not at all.
/// </para>
/// <para>
/// A dropped activity still exists for propagation: its trace id keeps correlating the logs.
/// Set explicitly, the sampler outranks <c>OTEL_TRACES_SAMPLER</c>, which would follow the
/// caller's flag.
/// </para>
/// </remarks>
internal static class TraceSampling
{
    /// <summary>The sampler recording <paramref name="ratio"/> of the traces.</summary>
    public static Sampler Of(double ratio)
    {
        var byTraceId = new TraceIdRatioBasedSampler(ratio);
        return new ParentBasedSampler(
            byTraceId,
            remoteParentSampled: byTraceId,
            remoteParentNotSampled: byTraceId);
    }
}
