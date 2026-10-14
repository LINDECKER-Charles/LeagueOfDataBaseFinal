namespace LoDb.Api.Modules.Seo;

/// <summary>
/// Settings of the crawler-facing files (<c>LoDb:Seo</c>), checked when the host starts.
/// </summary>
internal sealed class SeoOptions
{
    public const string SectionName = "LoDb:Seo";

    /// <summary>
    /// Default of <see cref="SitemapVersionWindow"/>: the latest version alone, the one whose
    /// short URLs are canonical.
    /// </summary>
    public const int DefaultSitemapVersionWindow = 0;

    /// <summary>
    /// Origin every published URL points to, such as <c>https://league-of-data-base.com</c>;
    /// unset, the files use the origin of the request.
    /// </summary>
    /// <remarks>
    /// Unset suits a local stack or a preview, which must never advertise production. nginx
    /// already folds the <c>www.</c> and <c>.fr</c> hosts into the canonical one before a
    /// request reaches the API; setting the origin also shields the files from a stray
    /// <c>Host</c> header.
    /// </remarks>
    public string? CanonicalOrigin { get; set; }

    /// <summary>
    /// How many versions before the latest one the sitemap index announces; <c>0</c>, the
    /// default, leaves only the latest version's sitemaps.
    /// </summary>
    /// <remarks>
    /// A past version's sitemap lists only the pages still canonical there, those of the
    /// entities it was the last to hold (ADR 0005, amendment of 2026-10-14): every other page
    /// of a past version is indexed under its latest short URL, or not at all. A window then
    /// announces the entities the last patches dropped; the sitemap of a version outside it
    /// is still served.
    /// </remarks>
    public int SitemapVersionWindow { get; set; } = DefaultSitemapVersionWindow;

    /// <summary>
    /// Key of the IndexNow protocol, 8 to 128 letters, digits or dashes: set, the promotion
    /// of a patch submits the pages it changed to the search engines that share IndexNow
    /// (Bing, Yandex, Naver, Seznam…), and <c>/{key}.txt</c> proves the site holds it.
    /// </summary>
    /// <remarks>
    /// Public by design: the key file publishes it. Submitting also needs
    /// <see cref="CanonicalOrigin"/>, the host the URLs belong to: without either, nothing is
    /// submitted, as on a local stack or on staging, which no engine indexes.
    /// </remarks>
    public string? IndexNowKey { get; set; }
}
