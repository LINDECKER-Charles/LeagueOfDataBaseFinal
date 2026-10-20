namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>What became of the units of a version that left the latest rank.</summary>
/// <param name="PassId">The pass they joined or wait for; null when nothing was to add.</param>
/// <param name="Units">The units added: one per locale its passes did not cover yet.</param>
/// <param name="IsDeferred">
/// Whether they wait for the active pass to end: it renders one type of page only, and would
/// leave the others out.
/// </param>
internal sealed record UnitEnqueue(long? PassId, int Units, bool IsDeferred)
{
    public static UnitEnqueue Nothing { get; } = new(null, 0, false);

    public static UnitEnqueue Added(long passId, int units) => new(passId, units, false);

    public static UnitEnqueue Deferred(long passId) => new(passId, 0, true);
}
