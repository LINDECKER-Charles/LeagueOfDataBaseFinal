namespace LoDb.Infrastructure.Tests.Robots.Support;

/// <summary>User agents as their senders send them, for the tests of the robots journal.</summary>
internal static class UserAgents
{
    public const string Googlebot =
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    public const string GooglebotSmartphone =
        "Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, "
        + "like Gecko) Chrome/141.0.7390.122 Mobile Safari/537.36 (compatible; Googlebot/2.1; "
        + "+http://www.google.com/bot.html)";

    public const string Bingbot =
        "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)";

    public const string GptBot =
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; "
        + "+https://openai.com/gptbot)";

    public const string ChatGptUser =
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko); compatible; ChatGPT-User/1.0; "
        + "+https://openai.com/bot";

    public const string AhrefsBot =
        "Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)";

    public const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
        + "Chrome/141.0.0.0 Safari/537.36";

    public const string Firefox =
        "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";
}
