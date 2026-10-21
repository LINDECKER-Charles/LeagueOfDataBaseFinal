using LoDb.Api.Modules.Admin.Prerender.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>
/// The operator's choice of pace, stored in <c>prerender_pace_setting</c>: the mode, and the
/// target in renders a minute that the manual mode holds (kept while adaptive).
/// </summary>
internal sealed record PaceSetting(PaceChoice Mode, int RendersPerMinute)
{
    /// <summary>When an operator last set it; null while never set.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    public bool IsManual => Mode == PaceChoice.Manual;

    /// <summary>Never set: adaptive, its target the regulator's ceiling.</summary>
    public static PaceSetting DefaultOf(PaceOptions pace)
    {
        ArgumentNullException.ThrowIfNull(pace);
        return new PaceSetting(PaceChoice.Adaptive, pace.MaxRendersPerMinute);
    }

    /// <summary>Whether the pass runs alike under both: same mode, same target.</summary>
    public bool Means(PaceSetting other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Mode == other.Mode && RendersPerMinute == other.RendersPerMinute;
    }
}
