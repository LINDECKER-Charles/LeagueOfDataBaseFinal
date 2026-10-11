namespace LoDb.Desktop.Hosting;

/// <summary>
/// The Content-Security-Policy of the shell's index: the page holds the bridge to the host
/// (files, system browser, updates), so it runs the shell's own bundles and the exact inline
/// scripts it was served with, and nothing else.
/// </summary>
/// <remarks>
/// The API is reached through the host's proxy, on the page's own origin; images and videos
/// may also come from the remote API's origin and from Riot's CDNs, as on the web. No
/// <c>'unsafe-eval'</c>: the i18n messages are compiled at build time.
/// </remarks>
internal static class ShellPolicy
{
    private const string Separator = "; ";

    // The Riot CDNs the pages hotlink: Data Dragon art, CommunityDragon previews, ability videos.
    private const string RiotMedia =
        "https://ddragon.leagueoflegends.com https://raw.communitydragon.org "
        + "https://d28xe8vt774jo5.cloudfront.net";

    /// <summary>
    /// The policy of a page whose inline scripts <paramref name="scriptHashes"/> admits.
    /// </summary>
    public static string For(IEnumerable<string> scriptHashes, Uri apiOrigin)
    {
        ArgumentNullException.ThrowIfNull(apiOrigin);
        var api = apiOrigin.GetLeftPart(UriPartial.Authority);
        return string.Join(
            Separator,
            "default-src 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "frame-src 'none'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            string.Join(' ', ["script-src 'self'", .. scriptHashes]),
            "style-src 'self' 'unsafe-inline'",
            $"img-src 'self' data: {api} {RiotMedia}",
            $"media-src 'self' {api} {RiotMedia}",
            "font-src 'self'",
            "connect-src 'self'",
            "worker-src 'self'",
            "manifest-src 'self'");
    }
}
