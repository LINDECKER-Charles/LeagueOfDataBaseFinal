using LoDb.Api.Modules.Catalog.Http;

namespace LoDb.Api.Modules.Legacy;

/// <summary>The failures of the redirects that the catalog's own problems do not cover.</summary>
internal static class LegacyProblems
{
    /// <summary>No old route had this path: a real 404, which nginx renders as a page.</summary>
    public static CatalogProblem UnknownPath() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = "unknown-legacy-url",
        Title = "No former URL of the site has this path.",
    };
}
