using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>The words of a probe result, as the legacy admin showed them.</summary>
internal static class ProbeStatuses
{
    public const string Ok = "ok";
    public const string Degraded = "degraded";
    public const string Down = "down";

    public static string Of(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => Ok,
        HealthStatus.Degraded => Degraded,
        _ => Down,
    };
}
