using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// The origin the crawler-facing files write their URLs with, without a trailing slash.
/// </summary>
/// <remarks>
/// The request's own origin unless <see cref="SeoOptions.CanonicalOrigin"/> is set. Behind
/// nginx the scheme comes from <c>X-Forwarded-Proto</c>, trusted from nginx's network only,
/// and the host is the one nginx received once <c>www.</c> and <c>.fr</c> are folded.
/// </remarks>
internal sealed class SiteOrigin(IOptions<SeoOptions> options)
{
    public string Of(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configured = options.Value.CanonicalOrigin;
        return string.IsNullOrEmpty(configured)
            ? $"{request.Scheme}://{request.Host.ToUriComponent()}"
            : SeoOptionsValidator.Normalize(configured);
    }
}
