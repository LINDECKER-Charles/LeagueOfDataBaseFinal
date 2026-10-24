namespace LoDb.Infrastructure.Robots.Identification;

/// <summary>
/// What a robot does, the vocabulary of the reports: the crawlers that explore the site on
/// their own, the fetchers a person sets off, the services and the tools.
/// </summary>
public static class RobotCategories
{
    /// <summary>Search engines: they index the pages.</summary>
    public const string Search = "search";

    /// <summary>Collectors of AI companies: training data or the index of an assistant.</summary>
    public const string Ai = "ai";

    /// <summary>Fetchers a person sets off: an assistant answering, a feed, a reading.</summary>
    public const string Assistant = "assistant";

    /// <summary>SEO and marketing tools, advertising checks.</summary>
    public const string Seo = "seo";

    /// <summary>Previews of a link shared on a network or a messenger.</summary>
    public const string Social = "social";

    /// <summary>Uptime monitors and audits (Lighthouse, validators).</summary>
    public const string Monitoring = "monitoring";

    /// <summary>Web archives.</summary>
    public const string Archive = "archive";

    /// <summary>HTTP libraries, scripts, headless browsers and scanners.</summary>
    public const string Tool = "tool";

    /// <summary>A robot by its user agent, which the catalog does not know.</summary>
    public const string Unidentified = "unidentified";

    /// <summary>A browser's user agent whose pace no reader keeps.</summary>
    public const string Undeclared = "undeclared";

    /// <summary>Every category, in the order the reports list them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Search, Ai, Assistant, Seo, Social, Monitoring, Archive, Tool, Unidentified, Undeclared,
    ];

    /// <summary>Whether robots of the category explore the site on their own.</summary>
    public static bool IsCrawler(string category) =>
        category is Search or Ai or Seo or Archive or Unidentified or Undeclared;
}
