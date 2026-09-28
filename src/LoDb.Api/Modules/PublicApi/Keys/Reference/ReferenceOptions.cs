namespace LoDb.Api.Modules.PublicApi.Keys.Reference;

/// <summary>
/// Settings of the documentation of the public API (<c>LoDb:PublicApi:Reference</c>),
/// checked when the host starts.
/// </summary>
internal sealed class ReferenceOptions
{
    public const string SectionName = PublicApiOptions.SectionName + ":Reference";

    /// <summary>
    /// Origin the documentation gives for <c>/v1</c> and <c>/healthz</c>, such as
    /// <c>https://api.league-of-data-base.com</c>: the subdomain nginx serves both on. Unset,
    /// the site's origin (<see cref="PublicApiOptions.SiteOrigin"/>), which routes
    /// <c>/v1</c> only.
    /// </summary>
    public string? BaseUrl { get; set; }
}
