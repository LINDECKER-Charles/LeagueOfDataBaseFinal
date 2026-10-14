namespace LoDb.Api.Modules.Seo;

/// <summary>How long the crawler-facing documents may be reused.</summary>
internal static class SeoCaching
{
    /// <summary>
    /// Documents that follow the patch cadence (index, sitemaps, robots, llms): an hour keeps
    /// crawlers cheap and current.
    /// </summary>
    public const string Hour = "public, max-age=3600";
}
