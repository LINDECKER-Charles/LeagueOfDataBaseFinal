namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// One entry of a sitemap or of the sitemap index: its absolute location and, when it is
/// known, the last time its content changed.
/// </summary>
/// <param name="Location">The absolute URL of the page or of the sitemap.</param>
/// <param name="LastModified">
/// The promotion of the latest patch for a page that patch changed; null when no date is
/// known, which is better than a guessed one.
/// </param>
internal sealed record SitemapEntry(string Location, DateTimeOffset? LastModified = null);
