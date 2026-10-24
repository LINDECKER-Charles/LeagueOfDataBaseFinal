using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// The collectors of AI companies, as their operators document their tokens (the same list
/// as nginx's <c>$lodb_ai_crawler</c>, docker/nginx/nginx.conf, and more).
/// </summary>
internal static class AiRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Ai,
        [
            new("gptbot", "GPTBot", "OpenAI", "gptbot"),
            new("oai-searchbot", "OAI-SearchBot", "OpenAI", "oai-searchbot"),
            new("claude-searchbot", "Claude-SearchBot", "Anthropic", "claude-searchbot"),
            new("claudebot", "ClaudeBot", "Anthropic", "claudebot|anthropic-ai|claude-web"),
            new("perplexitybot", "PerplexityBot", "Perplexity", "perplexitybot"),
            new("google-cloudvertexbot", "Google-CloudVertexBot", "Google",
                "google-cloudvertexbot", Google),
            new("ccbot", "CCBot", "Common Crawl", "ccbot"),
            new("bytespider", "Bytespider", "ByteDance", "bytespider"),
            new("tiktokspider", "TikTokSpider", "ByteDance", "tiktokspider"),
            new("meta-externalagent", "Meta External Agent", "Meta",
                "meta-externalagent|facebookbot"),
            new("amazonbot", "Amazonbot", "Amazon", "amazonbot", Amazon),
            new("cohere", "Cohere", "Cohere", "cohere-ai|cohere-training-data-crawler"),
            new("diffbot", "Diffbot", "Diffbot", "diffbot"),
            new("ai2bot", "AI2Bot", "Allen Institute for AI", "ai2bot"),
            new("youbot", "YouBot", "You.com", "youbot"),
            new("timpibot", "Timpibot", "Timpi", "timpibot"),
            new("imagesiftbot", "ImagesiftBot", "ImageSift", "imagesiftbot"),
            new("omgili", "Omgilibot", "Webz.io", "omgili"),
            new("pangubot", "PanguBot", "Huawei", "pangubot"),
            new("iaskspider", "iAskSpider", "iAsk", "iaskspider"),
            new("kangaroo-bot", "Kangaroo Bot", "Kangaroo LLM", "kangaroo bot"),
            new("img2dataset", "img2dataset", "LAION", "img2dataset"),
        ]);
}
