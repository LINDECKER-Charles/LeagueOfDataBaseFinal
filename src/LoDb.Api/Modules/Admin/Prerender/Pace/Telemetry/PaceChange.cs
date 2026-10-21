namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>
/// A move of the regulator, the <c>change</c> of <c>lodb_prerender_pace_changes_total</c>.
/// </summary>
internal enum PaceChange
{
    Increased,
    Decreased,
    Held,
    Released,
}
