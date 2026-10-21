namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>How the pass sets its pace (<see cref="PaceOptions.Mode"/>).</summary>
internal enum PaceMode
{
    /// <summary>
    /// The regulator sets it between the floor and the ceiling, on the CPU the host and the
    /// visitors leave free.
    /// </summary>
    Adaptive,

    /// <summary>
    /// The kill switch: <see cref="PrerenderOptions.RendersPerMinute"/>, whatever the load,
    /// without reading anything.
    /// </summary>
    Fixed,
}
