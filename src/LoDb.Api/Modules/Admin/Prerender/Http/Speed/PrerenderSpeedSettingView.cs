namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>The operator's choice of pace, as stored.</summary>
internal sealed record PrerenderSpeedSettingView
{
    /// <summary><c>adaptive</c> or <c>manual</c>.</summary>
    public required string Mode { get; init; }

    /// <summary>The manual target in renders a minute, bounded by the hard maximum.</summary>
    public required int RendersPerMinute { get; init; }

    /// <summary>When an operator last set it; null while never set (adaptive by default).</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
