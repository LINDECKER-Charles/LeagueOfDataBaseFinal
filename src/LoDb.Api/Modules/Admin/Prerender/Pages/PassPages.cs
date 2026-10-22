namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// Which pages of each (version, locale) a pass renders; stored as the scope's <c>pages</c>
/// (<see cref="PassScopeJson.OpenersPages"/>).
/// </summary>
internal enum PassPages
{
    /// <summary>
    /// Every page of the version (<c>VersionPages</c>): its lists, its patch page and every
    /// entity.
    /// </summary>
    Every,

    /// <summary>
    /// The patch page and the pages that open a state at the version, open or closed: the pages
    /// of the index that may be indexed, rendered first, before a pass over every page. A set
    /// that no promotion changes once the version is indexed, so a unit's cursor stays valid.
    /// </summary>
    Openers,
}
