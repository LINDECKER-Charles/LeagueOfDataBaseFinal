using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.States.Details;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The state fields of a detail, or the problem that answers instead.</summary>
internal sealed record StateRead
{
    private StateRead(DetailState? state, CatalogProblem? problem)
    {
        State = state;
        Problem = problem;
    }

    public DetailState? State { get; }

    public CatalogProblem? Problem { get; }

    [MemberNotNullWhen(true, nameof(State))]
    [MemberNotNullWhen(false, nameof(Problem))]
    public bool IsRead => State is not null;

    public static StateRead Read(DetailState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new StateRead(state, null);
    }

    public static StateRead Fail(CatalogProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new StateRead(null, problem);
    }
}
