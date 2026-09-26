namespace LoDb.Infrastructure.Persistence.Apps;

/// <summary>
/// A row of <c>client_policy</c>: the policy of one app, as last published (ADR 0008).
/// </summary>
/// <remarks>
/// Mutable state, hence in the database (ADR 0004): republishing a row withdraws a release
/// or a bundle without a new release. No row means no policy for the app. Versions are
/// three numbers, as the release tags write them. The bundle columns are all set or all
/// null, and only Android has a bundle.
/// </remarks>
public sealed class ClientPolicyEntry
{
    public const int VersionMaxLength = 32;
    public const int BundleIdMaxLength = 64;
    public const int BundleUrlMaxLength = 2048;
    public const int BundleSignatureMaxLength = 1024;

    public AppPlatform Platform { get; set; }

    /// <summary>Below it the app must update (426); null while there is no floor.</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>The newest release; null while none is published.</summary>
    public string? LatestVersion { get; set; }

    /// <summary>Id of the current live update bundle, as the live update plugin names it.</summary>
    public string? BundleId { get; set; }

    /// <summary>Where the app downloads the bundle (a GitHub Release asset).</summary>
    public string? BundleUrl { get; set; }

    /// <summary>SHA-256 of the bundle archive, in lowercase hexadecimal.</summary>
    public string? BundleChecksum { get; set; }

    /// <summary>RSA signature of the bundle, in base64.</summary>
    public string? BundleSignature { get; set; }

    /// <summary>Oldest native shell the bundle runs on.</summary>
    public string? BundleMinimumNativeVersion { get; set; }

    public DateTimeOffset PublishedAt { get; set; }
}
