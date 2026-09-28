namespace LoDb.Api.Modules.ClientPolicy.Versions;

/// <summary>
/// <c>X-LoDb-Client: {platform}/{version}</c>, which every request of an app carries
/// (ADR 0008) and the web never sends.
/// </summary>
internal static class ClientHeader
{
    public const string Name = "X-LoDb-Client";

    private const char Separator = '/';

    /// <summary>
    /// The app the header names; null without a header, or with one that names no known app
    /// in a readable version, which the API then treats as the web.
    /// </summary>
    public static AppClient? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = request.Headers[Name];
        return values.Count == 1 ? Parse(values[0]) : null;
    }

    public static AppClient? Parse(string? value)
    {
        var separator = value?.IndexOf(Separator, StringComparison.Ordinal) ?? -1;
        if (value is null || separator < 0)
        {
            return null;
        }

        return ClientPlatforms.TryParse(value[..separator], out var platform)
            && ClientVersion.TryParse(value[(separator + 1)..], out var version)
                ? new AppClient(platform, version)
                : null;
    }
}
