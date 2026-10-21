using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>The regulator as the admin and the metrics see it, while a pass drains here.</summary>
internal sealed record PaceSnapshot
{
    /// <summary>The pass being drained.</summary>
    public required long PassId { get; init; }

    public required PaceMode Mode { get; init; }

    public required PaceState State { get; init; }

    /// <summary>The allowance in force now: 0 while held, the floor once stale.</summary>
    public required int RendersPerMinute { get; init; }

    public required PaceReason Reason { get; init; }

    /// <summary>The host's busy share over the last minute, at the last decision.</summary>
    public double? HostBusy { get; init; }

    public DateTimeOffset? HeldUntil { get; init; }

    public required DateTimeOffset Since { get; init; }
}
