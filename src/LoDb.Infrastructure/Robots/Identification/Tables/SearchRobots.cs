using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>The crawlers of search engines, the most specific token of an operator first.</summary>
internal static class SearchRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Search,
        [
            new("googlebot-image", "Googlebot Image", "Google", "googlebot-image", Google),
            new("googlebot-video", "Googlebot Video", "Google", "googlebot-video", Google),
            new("googlebot-news", "Googlebot News", "Google", "googlebot-news", Google),
            new("storebot-google", "Storebot", "Google", "storebot-google", Google),
            new("google-inspectiontool", "Google Inspection Tool", "Google",
                "google-inspectiontool", Google),
            new("googleother", "GoogleOther", "Google", "googleother", Google),
            new("googlebot", "Googlebot", "Google", "googlebot", Google),
            new("bingbot", "Bingbot", "Microsoft", "bingbot|msnbot", Bing),
            new("applebot", "Applebot", "Apple", "applebot", Apple),
            new("yandexbot", "YandexBot", "Yandex",
                "yandexbot|yandeximages|yandexmobilebot|yandex.com/bots", Yandex),
            new("baiduspider", "Baiduspider", "Baidu", "baiduspider", Baidu),
            new("duckduckbot", "DuckDuckBot", "DuckDuckGo", "duckduckbot"),
            new("seznambot", "SeznamBot", "Seznam", "seznambot", Seznam),
            new("petalbot", "PetalBot", "Huawei", "petalbot", Petal),
            new("qwantbot", "Qwantbot", "Qwant", "qwantbot|qwantify"),
            new("yeti", "Yeti", "Naver", "yeti/|naver.me"),
            new("sogou", "Sogou Spider", "Sogou", "sogou web spider|sogou inst spider"),
            new("coccocbot", "coccocbot", "Cốc Cốc", "coccocbot"),
            new("mojeekbot", "MojeekBot", "Mojeek", "mojeekbot"),
            new("slurp", "Yahoo Slurp", "Yahoo", "slurp"),
            new("seekportbot", "SeekportBot", "Seekport", "seekportbot"),
            new("yisouspider", "YisouSpider", "Alibaba", "yisouspider"),
        ]);
}
