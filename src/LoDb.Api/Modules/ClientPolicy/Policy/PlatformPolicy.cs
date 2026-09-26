using LoDb.Infrastructure.Persistence.Apps;

namespace LoDb.Api.Modules.ClientPolicy.Policy;

/// <summary>What one app must run, and what it may update to.</summary>
internal sealed record PlatformPolicy
{
    public required ClientPlatform Platform { get; init; }

    /// <summary>Below it the app must update first (426); null while there is no floor.</summary>
    public string? MinimumVersion { get; init; }

    /// <summary>The newest release; null while none is published.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Android only: the current live update bundle; null while there is none.</summary>
    public LiveUpdateBundle? Bundle { get; init; }

    /// <summary>When the policy was last published; null while it never was.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The policy of an app nobody published yet: no floor, no release.</summary>
    public static PlatformPolicy Unpublished(ClientPlatform platform) =>
        new() { Platform = platform };

    public static PlatformPolicy Of(ClientPlatform platform, ClientPolicyEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new PlatformPolicy
        {
            Platform = platform,
            MinimumVersion = row.MinimumVersion,
            LatestVersion = row.LatestVersion,
            Bundle = BundleOf(row),
            PublishedAt = row.PublishedAt,
        };
    }

    // The table holds the five bundle columns all set or all null.
    private static LiveUpdateBundle? BundleOf(ClientPolicyEntry row) =>
        row is
        {
            BundleId: { } id,
            BundleUrl: { } url,
            BundleChecksum: { } checksum,
            BundleSignature: { } signature,
            BundleMinimumNativeVersion: { } native,
        }
            ? new LiveUpdateBundle
            {
                Id = id,
                Url = url,
                Checksum = checksum,
                Signature = signature,
                MinimumNativeVersion = native,
            }
            : null;
}
