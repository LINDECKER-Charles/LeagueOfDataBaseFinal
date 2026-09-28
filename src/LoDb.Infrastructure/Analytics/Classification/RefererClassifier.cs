namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>
/// The legacy stack's referrer classification (<c>RefererClassifier</c>), ported as is: the
/// site itself and its subdomains, then search engines, then social networks, anything else
/// external.
/// </summary>
/// <remarks>
/// The host is read as PHP's <c>parse_url</c> reads it: after a scheme and <c>//</c>, or
/// after a leading <c>//</c>, without user info or port. A referrer without one is direct.
/// </remarks>
internal static class RefererClassifier
{
    private const string AuthorityMarker = "//";
    private const string SchemeEnd = ":";
    private const char UserInfoEnd = '@';
    private const char PortStart = ':';
    private const char LiteralStart = '[';
    private const char LiteralEnd = ']';
    private const char SubdomainSeparator = '.';

    private static readonly char[] AuthorityEnds = ['/', '?', '#'];

    private static readonly string[] SearchHosts =
    [
        "google.", "bing.com", "duckduckgo.com", "yahoo.", "yandex.",
        "baidu.com", "ecosia.org", "qwant.com", "startpage.com", "brave.com",
    ];

    private static readonly string[] SocialHosts =
    [
        "facebook.com", "fb.com", "twitter.com", "x.com", "t.co", "reddit.com",
        "youtube.com", "youtu.be", "instagram.com", "tiktok.com", "linkedin.com",
        "discord.com", "discord.gg", "pinterest.", "twitch.tv", "mastodon.",
    ];

    /// <param name="referer">The <c>Referer</c> header, if any.</param>
    /// <param name="appHost">Host the page was requested on, without its port.</param>
    public static RefererOrigin Classify(string? referer, string appHost)
    {
        ArgumentNullException.ThrowIfNull(appHost);
        var text = LegacyText.Trim(referer);
        var host = text.Length == 0 ? null : HostOf(text);
        if (string.IsNullOrEmpty(host))
        {
            return RefererOrigin.Direct;
        }

        var lower = LegacyText.Lower(host);
        return new RefererOrigin(lower, SourceOf(lower, LegacyText.Lower(appHost)));
    }

    private static string SourceOf(string host, string appHost)
    {
        if (IsSite(host, appHost))
        {
            return RefererSources.Internal;
        }

        if (Contains(host, SearchHosts))
        {
            return RefererSources.Search;
        }

        return Contains(host, SocialHosts) ? RefererSources.Social : RefererSources.External;
    }

    private static bool IsSite(string host, string appHost) =>
        appHost.Length > 0
        && (host == appHost
            || host.EndsWith(SubdomainSeparator + appHost, StringComparison.Ordinal));

    private static bool Contains(string host, string[] needles) =>
        Array.Exists(needles, needle => host.Contains(needle, StringComparison.Ordinal));

    private static string? HostOf(string url)
    {
        var start = AuthorityStart(url);
        if (start < 0)
        {
            return null;
        }

        var end = url.IndexOfAny(AuthorityEnds, start);
        var authority = end < 0 ? url[start..] : url[start..end];
        authority = authority[(authority.LastIndexOf(UserInfoEnd) + 1)..];
        if (authority.StartsWith(LiteralStart))
        {
            var close = authority.IndexOf(LiteralEnd, StringComparison.Ordinal);
            return close < 0 ? authority : authority[..(close + 1)];
        }

        var port = authority.IndexOf(PortStart, StringComparison.Ordinal);
        return port < 0 ? authority : authority[..port];
    }

    // Where the authority begins, or -1: "scheme://…" or "//…".
    private static int AuthorityStart(string url)
    {
        if (url.StartsWith(AuthorityMarker, StringComparison.Ordinal))
        {
            return AuthorityMarker.Length;
        }

        var marker = url.IndexOf(SchemeEnd + AuthorityMarker, StringComparison.Ordinal);
        return marker > 0 && IsScheme(url[..marker])
            ? marker + SchemeEnd.Length + AuthorityMarker.Length
            : -1;
    }

    private static bool IsScheme(string scheme) =>
        char.IsAsciiLetter(scheme[0])
        && scheme.All(static c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
}
