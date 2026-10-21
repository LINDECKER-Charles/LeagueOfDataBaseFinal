namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>The pace the operator sets from the admin.</summary>
internal sealed record PrerenderSpeedRequest
{
    /// <summary><c>adaptive</c> or <c>manual</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>
    /// The manual target in renders a minute, from 1 to the hard maximum; required in manual
    /// mode, kept as it was when absent in adaptive mode.
    /// </summary>
    public int? RendersPerMinute { get; init; }
}
