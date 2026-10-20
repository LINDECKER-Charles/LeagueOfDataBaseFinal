using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.History;

/// <summary>A pass as the database holds it, its units by state and its author's name.</summary>
internal sealed record PassFacts
{
    public required PrerenderPass Pass { get; init; }

    /// <summary>The number of its units in each state; a state none is in is absent.</summary>
    public required IReadOnlyDictionary<PrerenderUnitState, int> Units { get; init; }

    public string? AuthorName { get; init; }

    public int UnitsIn(PrerenderUnitState state) => Units.GetValueOrDefault(state);
}
