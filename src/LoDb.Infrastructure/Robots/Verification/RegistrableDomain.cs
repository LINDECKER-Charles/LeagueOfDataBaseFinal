namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>
/// The domain a host name was registered under, the part that names who runs it:
/// <c>googlebot.com</c> of <c>crawl-66-249-66-1.googlebot.com</c>, <c>amazonaws.com</c> of
/// an EC2 name, <c>example.co.uk</c> under a second-level suffix.
/// </summary>
/// <remarks>
/// Not the public suffix list: a country code with a common second level (<c>co</c>,
/// <c>com</c>, <c>net</c>…) keeps three labels, anything else two. Enough to tell networks
/// apart; the verification itself compares whole names.
/// </remarks>
internal static class RegistrableDomain
{
    private const char Separator = '.';
    private const int CountryCodeLength = 2;
    private const int PlainLabels = 2;
    private const int SecondLevelLabels = 3;

    private static readonly HashSet<string> SecondLevels = new(StringComparer.Ordinal)
    {
        "ac", "co", "com", "edu", "go", "gob", "gov", "ne", "net", "or", "org",
    };

    /// <returns>Null for a name of a single label.</returns>
    public static string? Of(string? host)
    {
        var labels = (host ?? string.Empty).Trim().TrimEnd(Separator).ToLowerInvariant()
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < PlainLabels)
        {
            return null;
        }

        var kept = labels.Length >= SecondLevelLabels
            && labels[^1].Length == CountryCodeLength
            && SecondLevels.Contains(labels[^2])
                ? SecondLevelLabels
                : PlainLabels;
        return string.Join(Separator, labels[^kept..]);
    }
}
