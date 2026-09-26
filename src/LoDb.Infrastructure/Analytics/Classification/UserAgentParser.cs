using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>
/// The legacy stack's small user agent heuristic (<c>UserAgentParser</c>), ported as is so
/// that the audiences of both stacks line up: robots first, then the browser and the system
/// from ordered tables, most specific token first (Edg before Chrome, Chrome before Safari).
/// </summary>
internal static partial class UserAgentParser
{
    private const string Android = "Android";
    private const string Mobile = "Mobile";
    private const string Tablet = "tablet";
    private const string Phone = "mobile";
    private const string Desktop = "desktop";

    private static readonly string[] BotMarkers =
    [
        "bot", "crawl", "spider", "slurp", "bingpreview", "facebookexternalhit",
        "embedly", "quora", "pinterest", "headless", "phantom", "puppeteer",
        "lighthouse", "python-requests", "curl", "wget", "go-http", "okhttp",
        "monitor", "uptime", "pingdom", "semrush", "ahrefs", "dataprovider",
    ];

    private static readonly (string Needle, string Label)[] Browsers =
    [
        ("Edg", "Edge"), ("OPR", "Opera"), ("Opera", "Opera"),
        ("SamsungBrowser", "Samsung Internet"), ("YaBrowser", "Yandex"),
        ("Vivaldi", "Vivaldi"), ("Brave", "Brave"), ("Firefox", "Firefox"),
        ("Chrome", "Chrome"), ("CriOS", "Chrome"), ("Safari", "Safari"),
        ("MSIE", "Internet Explorer"), ("Trident", "Internet Explorer"),
    ];

    private static readonly (string Needle, string Label)[] Systems =
    [
        ("Windows NT", "Windows"), ("iPhone", "iOS"), ("iPad", "iPadOS"),
        (Android, Android), ("CrOS", "ChromeOS"), ("Mac OS X", "macOS"),
        ("Macintosh", "macOS"), ("Linux", "Linux"),
    ];

    public static UserAgentProfile Parse(string? userAgent)
    {
        var text = LegacyText.Trim(userAgent);
        if (text.Length == 0)
        {
            return UserAgentProfile.Unidentified;
        }

        if (LooksLikeBot(text))
        {
            return UserAgentProfile.Robot;
        }

        return new UserAgentProfile(
            Match(Browsers, text),
            Match(Systems, text),
            DeviceOf(text),
            IsBot: false);
    }

    private static bool LooksLikeBot(string userAgent)
    {
        var lower = LegacyText.Lower(userAgent);
        return Array.Exists(
            BotMarkers,
            marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    private static string DeviceOf(string userAgent)
    {
        if (TabletPattern().IsMatch(userAgent)
            || (userAgent.Contains(Android, StringComparison.Ordinal)
                && !userAgent.Contains(Mobile, StringComparison.Ordinal)))
        {
            return Tablet;
        }

        return PhonePattern().IsMatch(userAgent) ? Phone : Desktop;
    }

    private static string Match((string Needle, string Label)[] table, string userAgent)
    {
        foreach (var (needle, label) in table)
        {
            if (userAgent.Contains(needle, StringComparison.Ordinal))
            {
                return label;
            }
        }

        return UserAgentProfile.Unknown;
    }

    [GeneratedRegex(
        "iPad|Tablet|Nexus 7|Nexus 10|Kindle|Silk|PlayBook",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TabletPattern();

    [GeneratedRegex(
        "Mobi|iPhone|iPod|Windows Phone|IEMobile|BlackBerry",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
