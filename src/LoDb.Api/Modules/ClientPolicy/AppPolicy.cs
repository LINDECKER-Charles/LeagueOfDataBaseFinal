namespace LoDb.Api.Modules.ClientPolicy;

/// <summary><c>GET /api/client-policy</c>: the policy of every app, one entry each.</summary>
internal sealed record AppPolicy
{
    public required IReadOnlyList<PlatformPolicy> Platforms { get; init; }

    public static AppPolicy Of(ClientPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AppPolicy
        {
            Platforms = [.. Enum.GetValues<ClientPlatform>().Select(platform =>
                new PlatformPolicy
                {
                    Platform = platform,
                    MinimumVersion = options.Of(platform).MinimumVersion,
                    LatestVersion = options.Of(platform).LatestVersion,
                })],
        };
    }
}
