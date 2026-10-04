using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Catalog.Http;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The catalog a call names, or the problem that answers instead.</summary>
internal sealed record CatalogRead
{
    private CatalogRead(CatalogContext? context, CatalogProblem? problem)
    {
        Context = context;
        Problem = problem;
    }

    public CatalogContext? Context { get; }

    public CatalogProblem? Problem { get; }

    [MemberNotNullWhen(true, nameof(Context))]
    [MemberNotNullWhen(false, nameof(Problem))]
    public bool IsOpen => Context is not null;

    public static CatalogRead Open(CatalogContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new CatalogRead(context, null);
    }

    public static CatalogRead Fail(CatalogProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new CatalogRead(null, problem);
    }
}
