using LoDb.Api.Modules.Seo.Pages;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The list a unit of a pass walks, in the order of <see cref="VersionPages"/>: the pages of its
/// version the scope keeps. Its cursor is an index in this list, and the queue reads where a
/// page stands in it the same way.
/// </summary>
internal static class UnitPages
{
    /// <summary>
    /// The pages of <paramref name="versionPages"/> a unit over <paramref name="scope"/> renders:
    /// those of its types (<see cref="PageTypes.Keep"/>), then, for an openers scope, only the
    /// patch page and the pages in <paramref name="openers"/>.
    /// </summary>
    /// <param name="versionPages">Every page of the version, in their order.</param>
    /// <param name="scope">The perimeter of the unit's pass.</param>
    /// <param name="openers">
    /// The paths of the states opened at the version (<c>items/3371-molten-edge</c>); read only
    /// for an openers scope.
    /// </param>
    public static IReadOnlyList<string> Of(
        IReadOnlyList<string> versionPages,
        PassScope scope,
        IReadOnlySet<string> openers)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(openers);
        var kept = PageTypes.Keep(versionPages, scope.PageTypes);
        return scope.Pages == PassPages.Openers
            ? [.. kept.Where(page => IsOpening(page, openers))]
            : kept;
    }

    private static bool IsOpening(string page, IReadOnlySet<string> openers) =>
        string.Equals(page, VersionPages.PatchChanges, StringComparison.Ordinal)
        || openers.Contains(page);
}
