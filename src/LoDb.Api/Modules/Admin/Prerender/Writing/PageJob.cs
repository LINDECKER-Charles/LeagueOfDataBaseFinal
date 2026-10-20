using LoDb.Api.Modules.Admin.Prerender.Units;

namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>One page of a unit: where the SSR renders it, and where the store keeps it.</summary>
/// <param name="Unit">The (version, locale) it belongs to.</param>
/// <param name="Path">Its path below <c>/{locale}/{version}/</c>: <c>items/1036-long-sword</c>.
/// </param>
internal sealed record PageJob(ClaimedUnit Unit, string Path)
{
    private const string PagesPrefix = "pages/";

    /// <summary>The page's path on the site: <c>/fr/16.19.1/items/1036-long-sword</c>.</summary>
    public string RequestPath =>
        $"/{Unit.LocaleCode}/{Unit.Version.Value}/{Path}";

    /// <summary>
    /// The page's key in the store, <c>pages/{epoch}</c> then its path on the site: the key
    /// nginx asks for, from the served epoch and the request's path.
    /// </summary>
    public string Key => $"{PagesPrefix}{Unit.Pass.Epoch}{RequestPath}";
}
