using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// Everything one evaluation read: a null source could not be read, and reads as missing.
/// </summary>
internal sealed record PaceReadings
{
    public required HostShares Host { get; init; }

    /// <summary>The API's throttled share of the wall time over a minute.</summary>
    public required double? ApiThrottled { get; init; }

    public required SsrSignals? Visitors { get; init; }

    public required SsrSignals? Pass { get; init; }

    public required PassLatencySample PassLatency { get; init; }
}
