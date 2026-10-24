namespace LoDb.Infrastructure.Robots.Identification;

/// <summary>
/// The domains each operator documents for the reverse DNS names of its crawlers: a genuine
/// address reverses to a name under one of them, which resolves back to it.
/// </summary>
/// <remarks>
/// Only operators that publish this method are listed. The others (OpenAI, Anthropic,
/// DuckDuckGo…) publish address ranges instead, which this journal does not fetch: their
/// robots are <c>unverifiable</c>.
/// </remarks>
internal static class VerificationDomains
{
    public static IReadOnlyList<string> Google { get; } =
        ["googlebot.com", "google.com", "googleusercontent.com"];

    public static IReadOnlyList<string> Bing { get; } = ["search.msn.com"];

    public static IReadOnlyList<string> Apple { get; } = ["applebot.apple.com"];

    public static IReadOnlyList<string> Yandex { get; } = ["yandex.ru", "yandex.net", "yandex.com"];

    public static IReadOnlyList<string> Baidu { get; } = ["baidu.com", "baidu.jp"];

    public static IReadOnlyList<string> Amazon { get; } = ["crawl.amazonbot.amazon"];

    public static IReadOnlyList<string> Seznam { get; } = ["seznam.cz"];

    public static IReadOnlyList<string> Petal { get; } = ["petalsearch.com"];
}
