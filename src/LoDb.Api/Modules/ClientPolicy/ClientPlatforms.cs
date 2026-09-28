using LoDb.Infrastructure.Persistence.Apps;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>
/// The names of the apps, as the header, the route and the command write them
/// (<c>desktop</c>, <c>android</c>), and their rows in <c>client_policy</c>.
/// </summary>
internal static class ClientPlatforms
{
    public const string Desktop = "desktop";
    public const string Android = "android";

    public static IReadOnlyList<ClientPlatform> All { get; } = Enum.GetValues<ClientPlatform>();

    /// <summary>Reads a name exactly as written above: the contract knows no other.</summary>
    public static bool TryParse(string? text, out ClientPlatform platform)
    {
        (var isKnown, platform) = text switch
        {
            Desktop => (true, ClientPlatform.Desktop),
            Android => (true, ClientPlatform.Android),
            _ => (false, default),
        };
        return isKnown;
    }

    public static string NameOf(ClientPlatform platform) => platform switch
    {
        ClientPlatform.Desktop => Desktop,
        ClientPlatform.Android => Android,
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
    };

    public static AppPlatform ToRow(ClientPlatform platform) => platform switch
    {
        ClientPlatform.Desktop => AppPlatform.Desktop,
        ClientPlatform.Android => AppPlatform.Android,
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
    };
}
