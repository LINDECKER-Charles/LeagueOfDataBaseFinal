using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// The pages of one version as the pass lists them (<see cref="IPrerenderCatalog.PagesAsync"/>):
/// whether a page is one of them, and where it stands in a pass's list.
/// </summary>
internal sealed class VersionPageIndex(IReadOnlyList<string> paths)
{
    private readonly HashSet<string> _paths = new(paths, StringComparer.Ordinal);

    /// <summary>Whether the catalogue lists <paramref name="pagePath"/>.</summary>
    public bool Lists(string pagePath) => _paths.Contains(pagePath);

    /// <summary>
    /// The index of <paramref name="pagePath"/> in the list a unit over
    /// <paramref name="types"/> walks (null for all of them): what its cursor counts; -1 when
    /// the list leaves it out.
    /// </summary>
    public int IndexIn(IReadOnlyList<ResourceType>? types, string pagePath)
    {
        var walked = PageTypes.Keep(paths, types);
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
