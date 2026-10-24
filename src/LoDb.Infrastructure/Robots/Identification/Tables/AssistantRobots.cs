using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// The fetchers a person sets off: an assistant reading a page to answer, a feed reader, a
/// page read aloud. They follow no link of their own.
/// </summary>
internal static class AssistantRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Assistant,
        [
            new("chatgpt-user", "ChatGPT-User", "OpenAI", "chatgpt-user"),
            new("claude-user", "Claude-User", "Anthropic", "claude-user"),
            new("perplexity-user", "Perplexity-User", "Perplexity", "perplexity-user"),
            new("mistralai-user", "MistralAI-User", "Mistral AI", "mistralai-user"),
            new("duckassistbot", "DuckAssistBot", "DuckDuckGo", "duckassistbot"),
            new("meta-externalfetcher", "Meta External Fetcher", "Meta", "meta-externalfetcher"),
            new("google-read-aloud", "Google Read Aloud", "Google", "google-read-aloud", Google),
            new("feedfetcher-google", "Feedfetcher", "Google", "feedfetcher-google", Google),
            new("googleimageproxy", "Google Image Proxy", "Google", "googleimageproxy", Google),
            new("google-pagerenderer", "Google PageRenderer", "Google",
                "google-pagerenderer", Google),
            new("apis-google", "APIs-Google", "Google", "apis-google", Google),
        ]);
}
