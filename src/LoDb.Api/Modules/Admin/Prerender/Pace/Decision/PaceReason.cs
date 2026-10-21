namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>What limits the pace now: the worst signal, or the regulator's own rule.</summary>
internal enum PaceReason
{
    None,
    WarmingUp,
    Ceiling,
    HostCpu,
    HostCpuSustained,
    HostSteal,
    VisitorsSsr,
    VisitorsRefused,
    ApiCpu,
    PassLatency,
    PassSsrPressure,
    SignalMissing,
    Fixed,
    Manual,
}
