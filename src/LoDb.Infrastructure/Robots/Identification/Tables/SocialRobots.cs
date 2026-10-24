using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// The previews of a link shared on a social network or a messenger. Never the token of an
/// app's own browser (Pinterest, Snapchat…), which a reader uses.
/// </summary>
internal static class SocialRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Social,
        [
            new("facebookexternalhit", "facebookexternalhit", "Meta",
                "facebookexternalhit|facebookcatalog"),
            new("bingpreview", "BingPreview", "Microsoft", "bingpreview", Bing),
            new("twitterbot", "Twitterbot", "X", "twitterbot"),
            new("discordbot", "Discordbot", "Discord", "discordbot"),
            new("slackbot", "Slackbot", "Slack", "slackbot|slack-imgproxy"),
            new("telegrambot", "TelegramBot", "Telegram", "telegrambot"),
            new("whatsapp", "WhatsApp", "Meta", "whatsapp"),
            new("linkedinbot", "LinkedInBot", "LinkedIn", "linkedinbot"),
            new("pinterestbot", "Pinterestbot", "Pinterest", "pinterestbot"),
            new("redditbot", "redditbot", "Reddit", "redditbot"),
            new("embedly", "Embedly", "Embedly", "embedly"),
            new("iframely", "Iframely", "Iframely", "iframely"),
            new("skypeuripreview", "Skype preview", "Microsoft", "skypeuripreview"),
            new("microsoftpreview", "MicrosoftPreview", "Microsoft", "microsoftpreview"),
            new("mastodon", "Mastodon", "Fediverse", "mastodon"),
            new("bluesky", "Bluesky Cardyb", "Bluesky", "cardyb"),
            new("vkshare", "VK Share", "VK", "vkshare"),
        ]);
}
