using System.ComponentModel.DataAnnotations;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// Tracing settings, bound from <c>LoDb:Telemetry</c>.
/// </summary>
internal sealed class TelemetryOptions
{
    public const string SectionName = "LoDb:Telemetry";

    /// <summary>
    /// One trace in twenty: enough to see where a slow render spends its time, while the
    /// collector and the trace store stay a small share of the host.
    /// </summary>
    public const double DefaultTraceSampleRatio = 0.05;

    private const double NoTrace = 0;
    private const double EveryTrace = 1;

    /// <summary>
    /// Share of the traces recorded and exported once an OTLP endpoint is set, decided from
    /// the trace id alone. Without an endpoint, none is recorded whatever this value.
    /// </summary>
    [Range(NoTrace, EveryTrace)]
    public double TraceSampleRatio { get; set; } = DefaultTraceSampleRatio;
}
