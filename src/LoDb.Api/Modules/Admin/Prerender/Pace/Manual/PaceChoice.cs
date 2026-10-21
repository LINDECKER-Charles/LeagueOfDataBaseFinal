namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>How the operator wants the pass paced, from the admin (<see cref="PaceSetting"/>).
/// </summary>
internal enum PaceChoice
{
    /// <summary>
    /// The regulator sets the pace on the CPU the host and the visitors leave free.
    /// </summary>
    Adaptive,

    /// <summary>
    /// The operator's target, held whatever the load but for the emergency brake
    /// (<see cref="ManualDecision"/>).
    /// </summary>
    Manual,
}
