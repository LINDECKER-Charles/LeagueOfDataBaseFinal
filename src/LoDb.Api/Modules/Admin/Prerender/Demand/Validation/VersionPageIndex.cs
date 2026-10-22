using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Paths;
using LoDb.Domain.States;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// The pages of one version as the pass lists them (<see cref="IPrerenderCatalog.PagesAsync"/>)
/// and its openers in the index (<see cref="IPrerenderCatalog.OpenersAsync"/>): whether a page
/// is one of them, and where it stands in the list a unit of a pass walks.
/// </summary>
internal sealed class VersionPageIndex(IReadOnlyList<string> paths, IReadOnlySet<string> openers)
{
    private const char Separator = '/';

    private readonly HashSet<string> _paths = new(paths, StringComparer.Ordinal);

    private Dictionary<string, string>? _entities;

    /// <summary>Whether the catalogue lists <paramref name="pagePath"/>.</summary>
    public bool Lists(string pagePath) => _paths.Contains(pagePath);

    /// <summary>
    /// The page of the same entity as <paramref name="pagePath"/>, spelled as the catalogue of
    /// this version lists it (the case of its id, its slug); null when it lists no such page.
    /// </summary>
    /// <remarks>
    /// The index of states keeps the path of a state as its opener spells it, for each of its
    /// versions; Data Dragon changed the case of some ids within a state (<c>FiddleSticks</c>,
    /// then <c>Fiddlesticks</c>), and a page asked for under another spelling redirects.
    /// </remarks>
    public string? SpellingOf(string pagePath)
    {
        ArgumentNullException.ThrowIfNull(pagePath);
        if (_paths.Contains(pagePath))
        {
            return pagePath;
        }

        var entities = LazyInitializer.EnsureInitialized(
            ref _entities,
            () => paths.DistinctBy(EntityOf, StringComparer.Ordinal)
                .ToDictionary(EntityOf, StringComparer.Ordinal));
        return entities.GetValueOrDefault(EntityOf(pagePath));
    }

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

    // The entity a page shows, its id folded as the index of states folds it; a list and the
    // patch page stand for themselves.
    private static string EntityOf(string pagePath)
    {
        var separator = pagePath.IndexOf(Separator, StringComparison.Ordinal);
        if (separator < 0 || !PageTypes.TryParse(pagePath[..separator], out var type))
        {
            return pagePath;
        }

        var id = CanonicalPath.IdOf(type, pagePath[(separator + 1)..]);
        return id is null ? pagePath : pagePath[..(separator + 1)] + EntityKey.Of(id);
    }
}
