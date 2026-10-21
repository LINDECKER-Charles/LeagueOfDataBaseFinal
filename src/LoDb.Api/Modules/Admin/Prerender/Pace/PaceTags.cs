using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>
/// The regulator's words outside the code: snake_case tags for the metrics and the logs,
/// kebab-case tokens for the admin's view.
/// </summary>
internal static class PaceTags
{
    private const char Snake = '_';
    private const char Kebab = '-';

    /// <summary>Every reason, in the order the metrics report them.</summary>
    public static IReadOnlyList<PaceReason> Reasons { get; } = Enum.GetValues<PaceReason>();

    /// <summary>The metric and log tag of <paramref name="reason"/>.</summary>
    public static string TagOf(PaceReason reason) => reason switch
    {
        PaceReason.None => "none",
        PaceReason.WarmingUp => "warming_up",
        PaceReason.Ceiling => "ceiling",
        PaceReason.HostCpu => "host_cpu",
        PaceReason.HostCpuSustained => "host_cpu_sustained",
        PaceReason.HostSteal => "host_steal",
        PaceReason.VisitorsSsr => "visitors_ssr",
        PaceReason.VisitorsRefused => "visitors_refused",
        PaceReason.ApiCpu => "api_cpu",
        PaceReason.PassLatency => "pass_latency",
        PaceReason.PassSsrPressure => "pass_ssr_pressure",
        PaceReason.SignalMissing => "signal_missing",
        PaceReason.Fixed => "fixed",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    /// <summary>The admin's token of <paramref name="reason"/>; null for none.</summary>
    public static string? TokenOf(PaceReason reason) =>
        reason == PaceReason.None ? null : TagOf(reason).Replace(Snake, Kebab);

    /// <summary>The admin's token of <paramref name="state"/>.</summary>
    public static string TokenOf(PaceState state) => state switch
    {
        PaceState.Starting => "starting",
        PaceState.Climbing => "climbing",
        PaceState.Steady => "steady",
        PaceState.Slowed => "slowed",
        PaceState.Held => "held",
        PaceState.Fixed => "fixed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>The metric tag of <paramref name="change"/>.</summary>
    public static string TagOf(PaceChange change) => change switch
    {
        PaceChange.Increased => "increased",
        PaceChange.Decreased => "decreased",
        PaceChange.Held => "held",
        PaceChange.Released => "released",
        _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
    };
}
