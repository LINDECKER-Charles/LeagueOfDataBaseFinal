namespace LoDb.Api.Modules.Admin.Prerender.Passes.Planning;

/// <summary>What a pass would render: its units in order, and what its scope left out.</summary>
/// <param name="Units">One per (version, locale), in the order the worker takes them.</param>
/// <param name="Skipped">(version, locale) left out as already complete in the epoch.</param>
/// <param name="Ignored">Versions asked for that are not older patches, in the order sent.</param>
internal sealed record PassPlan(
    IReadOnlyList<PlannedUnit> Units,
    int Skipped,
    IReadOnlyList<string> Ignored);
