using System.ComponentModel;
using LoDb.Infrastructure.Persistence.Apps;

namespace LoDb.Api.Modules.ClientPolicy.Policy;

/// <summary><c>GET /api/client-policy</c>: the policy of every app, one entry each.</summary>
/// <remarks>Immutable, so the cache hands the same instance to every request.</remarks>
[ImmutableObject(true)]
internal sealed record AppPolicy
{
    /// <summary>Every app, in the order of <see cref="ClientPlatform"/>.</summary>
    public required IReadOnlyList<PlatformPolicy> Platforms { get; init; }

    public static AppPolicy Of(IReadOnlyCollection<ClientPolicyEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new AppPolicy
        {
            Platforms = [.. ClientPlatforms.All.Select(platform =>
                rows.FirstOrDefault(row => row.Platform == ClientPlatforms.ToRow(platform))
                    is { } row
                    ? PlatformPolicy.Of(platform, row)
                    : PlatformPolicy.Unpublished(platform))],
        };
    }

    /// <summary>The policy of <paramref name="platform"/>.</summary>
    public PlatformPolicy For(ClientPlatform platform) =>
        Platforms.First(policy => policy.Platform == platform);
}
