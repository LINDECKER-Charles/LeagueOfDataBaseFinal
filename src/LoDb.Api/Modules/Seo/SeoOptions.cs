namespace LoDb.Api.Modules.Seo;

/// <summary>
/// Settings of the crawler-facing files (<c>LoDb:Seo</c>), checked when the host starts.
/// </summary>
internal sealed class SeoOptions
{
    public const string SectionName = "LoDb:Seo";

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
}
