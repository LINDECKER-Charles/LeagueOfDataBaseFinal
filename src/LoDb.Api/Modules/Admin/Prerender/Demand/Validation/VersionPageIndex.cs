using LoDb.Api.Modules.Admin.Prerender.Pages;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// The pages of one version as the pass lists them (<see cref="IPrerenderCatalog.PagesAsync"/>)
/// and its openers in the index (<see cref="IPrerenderCatalog.OpenersAsync"/>): whether a page
/// is one of them, and where it stands in the list a unit of a pass walks.
/// </summary>
internal sealed class VersionPageIndex(IReadOnlyList<string> paths, IReadOnlySet<string> openers)
{
    private readonly HashSet<string> _paths = new(paths, StringComparer.Ordinal);

    /// <summary>Whether the catalogue lists <paramref name="pagePath"/>.</summary>
    public bool Lists(string pagePath) => _paths.Contains(pagePath);

    /// <summary>
    /// The index of <paramref name="pagePath"/> in the list a unit over
    /// <paramref name="scope"/> walks (<see cref="UnitPages"/>): what its cursor counts; -1 when
    /// the list leaves it out.
    /// </summary>
    public int IndexIn(PassScope scope, string pagePath)
    {
        var walked = UnitPages.Of(paths, scope, openers);
        for (var index = 0; index < walked.Count; index++)
        {
            if (string.Equals(walked[index], pagePath, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
