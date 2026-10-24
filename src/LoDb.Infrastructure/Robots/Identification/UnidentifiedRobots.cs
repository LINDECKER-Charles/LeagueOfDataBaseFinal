using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.Robots.Identification;

/// <summary>
/// A robot the catalog does not know, by the marks of its user agent: a name ending in
/// <c>bot</c>, <c>crawler</c> or <c>spider</c>, a contact link or address. It is named after
/// its product token, so that its hits are told apart from another one's.
/// </summary>
internal static partial class UnidentifiedRobots
{
    public const string KeyPrefix = "unidentified:";

    private const int MaxNameLength = 48;
    private const string Compatible = "compatible;";
    private const char Replacement = '-';

    private static readonly string[] Markers =
    [
        "bot", "crawl", "spider", "slurp", "scrap", "fetch", "archiv", "+http", "http://",
        "https://", "@",
    ];

    private static readonly char[] TokenEnds = ['/', ';', ')', '(', ' '];

    /// <summary>A robot by a token of its user agent that says so: <c>ExampleBot/2.1</c>.</summary>
    /// <param name="agent">The user agent, trimmed.</param>
    public static RobotProfile? Named(string agent)
    {
        var match = RobotToken().Match(agent);
        return match.Success ? Profile(match.Value) : null;
    }

    /// <summary>
    /// A robot by a mark of its user agent alone (a contact link, a word), named after the
    /// token after <c>compatible;</c> or its first product token.
    /// </summary>
    /// <param name="agent">The user agent, trimmed.</param>
    /// <param name="lower">The same, lowercase.</param>
    public static RobotProfile? Marked(string agent, string lower) =>
        Array.Exists(Markers, marker => lower.Contains(marker, StringComparison.Ordinal))
            ? Of(agent)
            : null;

    /// <summary>
    /// The robot of an agent stored as a robot's that the catalog no longer tells apart from a
    /// browser's: its hits keep a name rather than vanish from the reports.
    /// </summary>
    public static RobotProfile Of(string agent) =>
        Profile(CompatibleToken(agent) ?? ProductToken(agent));

    private static RobotProfile Profile(string token)
    {
        var name = token.Length <= MaxNameLength ? token : token[..MaxNameLength];
        return new RobotProfile
        {
            Key = KeyPrefix + KeyOf(name),
            Name = name,
            Operator = string.Empty,
            Category = RobotCategories.Unidentified,
        };
    }

    // "Mozilla/5.0 (compatible; Name/1.0; +https://…)": the token after "compatible;".
    private static string? CompatibleToken(string agent)
    {
        var start = agent.IndexOf(Compatible, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var token = FirstToken(agent[(start + Compatible.Length)..].TrimStart());
        return token.Length == 0 ? null : token;
    }

    private static string ProductToken(string agent)
    {
        var token = FirstToken(agent);
        return token.Length == 0 ? agent : token;
    }

    private static string FirstToken(string text)
    {
        var end = text.IndexOfAny(TokenEnds);
        return end < 0 ? text : text[..end];
    }

    private static string KeyOf(string name) =>
        string.Concat(name.ToLowerInvariant().Select(static c =>
            char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : Replacement));

    [GeneratedRegex(
        "[a-z0-9][a-z0-9._-]*(?:bot|crawler|spider|scraper|fetcher|archiver)(?![a-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RobotToken();
}
