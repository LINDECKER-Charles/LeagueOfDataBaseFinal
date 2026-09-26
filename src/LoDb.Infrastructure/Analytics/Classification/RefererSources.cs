namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>
/// Where a view came from, as stored in <c>referer_source</c> and keyed in the reports: the
/// legacy values, kept stable.
/// </summary>
public static class RefererSources
{
    /// <summary>No usable referrer: a bookmark, a typed address, a stripped header.</summary>
    public const string Direct = "direct";

    /// <summary>A page of the site itself, an internal navigation included.</summary>
    public const string Internal = "internal";

    public const string Search = "search";

    public const string Social = "social";

    public const string External = "external";
}
