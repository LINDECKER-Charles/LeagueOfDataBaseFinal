using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Where the page of a detail's entity is canonical, or the problem that answers instead.
/// </summary>
internal sealed record CanonicalRead
{
    private CanonicalRead(CanonicalLink? link, CatalogProblem? problem)
    {
        Link = link;
        Problem = problem;
    }

    /// <summary>
    /// The canonical page once <see cref="IsLocated"/>; <see langword="null"/> then for a page
    /// that stays out of the index.
    /// </summary>
    public CanonicalLink? Link { get; }

    public CatalogProblem? Problem { get; }

    [MemberNotNullWhen(false, nameof(Problem))]
    public bool IsLocated => Problem is null;

    public static CanonicalRead Located(CanonicalLink? link) => new(link, null);

    public static CanonicalRead Fail(CatalogProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new CanonicalRead(null, problem);
    }
}
