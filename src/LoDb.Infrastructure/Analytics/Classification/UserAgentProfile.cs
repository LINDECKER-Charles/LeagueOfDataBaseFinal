namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>What <see cref="UserAgentParser"/> tells of a client, in legacy words.</summary>
/// <param name="Browser">Browser family, such as <c>Firefox</c>.</param>
/// <param name="Os">Operating system, such as <c>Android</c>.</param>
/// <param name="Device">
/// <c>desktop</c>, <c>mobile</c>, <c>tablet</c>, <c>bot</c> or <c>other</c>.
/// </param>
/// <param name="IsBot">Whether the client announces itself as a robot.</param>
internal sealed record UserAgentProfile(string Browser, string Os, string Device, bool IsBot)
{
    /// <summary>Neither the browser, the system nor the device could be told.</summary>
    public const string Unknown = "other";

    private const string BotBrowser = "Bot";
    private const string BotDevice = "bot";

    /// <summary>No user agent at all.</summary>
    public static UserAgentProfile Unidentified { get; } = new(Unknown, Unknown, Unknown, false);

    /// <summary>A robot keeps a device of its own, so that audiences leave it out.</summary>
    public static UserAgentProfile Robot { get; } = new(BotBrowser, Unknown, BotDevice, true);
}
