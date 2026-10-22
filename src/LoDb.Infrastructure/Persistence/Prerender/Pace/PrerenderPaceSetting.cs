namespace LoDb.Infrastructure.Persistence.Prerender.Pace;

/// <summary>
/// The single row of <c>prerender_pace_setting</c>: how the operator wants the pass paced,
/// adaptive (the regulator) or manual (a target in renders a minute), set live from the admin.
/// </summary>
/// <remarks>
/// Absent until an operator first sets it: the pass is then adaptive. The environment keeps the
/// kill switch (<c>LoDb:Prerender:Pace:Mode=fixed</c>) and the hard maximum of the target.
/// </remarks>
public sealed class PrerenderPaceSetting
{
    /// <summary>The only key the table accepts.</summary>
    public const short SingletonId = 1;

    /// <summary>The stored spelling of the adaptive mode.</summary>
    public const string AdaptiveMode = "adaptive";

    /// <summary>The stored spelling of the manual mode.</summary>
    public const string ManualMode = "manual";

    /// <summary>Longest stored mode.</summary>
    public const int ModeMaxLength = 16;

    public short Id { get; set; } = SingletonId;

    /// <summary><see cref="AdaptiveMode"/> or <see cref="ManualMode"/>.</summary>
    public required string Mode { get; set; }

    /// <summary>
    /// The manual target, kept while adaptive so that switching back finds it again.
    /// </summary>
    public int RendersPerMinute { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
