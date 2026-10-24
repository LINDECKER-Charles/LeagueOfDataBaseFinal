using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// The crawlers of SEO and marketing tools, and the checks of advertising networks.
/// </summary>
internal static class SeoRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Seo,
        [
            new("ahrefsbot", "AhrefsBot", "Ahrefs", "ahrefsbot|ahrefssiteaudit"),
            new("semrushbot", "SemrushBot", "Semrush", "semrushbot|siteauditbot|splitsignalbot"),
            new("mj12bot", "MJ12bot", "Majestic", "mj12bot"),
            new("dotbot", "DotBot", "Moz", "dotbot|rogerbot"),
            new("blexbot", "BLEXBot", "WebMeUp", "blexbot"),
            new("dataforseobot", "DataForSeoBot", "DataForSEO", "dataforseobot"),
            new("serpstatbot", "SerpstatBot", "Serpstat", "serpstatbot"),
            new("screaming-frog", "Screaming Frog", "Screaming Frog", "screaming frog"),
            new("sitebulb", "Sitebulb", "Sitebulb", "sitebulb"),
            new("barkrowler", "Barkrowler", "Babbar", "barkrowler"),
            new("seokicks", "SEOkicks", "SEOkicks", "seokicks"),
            new("awariobot", "AwarioBot", "Awario", "awariobot"),
            new("adsbot-google", "AdsBot", "Google", "adsbot-google", Google),
            new("mediapartners-google", "Mediapartners", "Google", "mediapartners-google", Google),
            new("adidxbot", "AdIdxBot", "Microsoft", "adidxbot", Bing),
        ]);
}
