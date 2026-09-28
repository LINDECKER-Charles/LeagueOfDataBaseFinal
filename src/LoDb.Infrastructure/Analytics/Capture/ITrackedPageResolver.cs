namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Tells which page an address shows: the grammar of the site's addresses and the catalog
/// belong to the API, which implements it.
/// </summary>
public interface ITrackedPageResolver
{
    /// <param name="target">The path and query requested: <c>/fr/items?lang=fr_FR</c>.</param>
    /// <param name="cancellationToken">Stops the lookups of the catalog.</param>
    /// <returns>
    /// Null when the address is not a counted page, or only redirects to one: the page it
    /// lands on is counted instead.
    /// </returns>
    Task<TrackedPage?> ResolveAsync(string target, CancellationToken cancellationToken);
}
