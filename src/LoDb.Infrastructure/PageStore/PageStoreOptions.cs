namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// The object store of the pre-rendered catalogue, as the API writes to it
/// (<c>LoDb:Prerender</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// <para>
/// The store stays dormant until <see cref="Endpoint"/>, <see cref="Bucket"/>,
/// <see cref="AccessKey"/> and <see cref="SecretKey"/> are all set: the hosts' overlay gives
/// the first two, the owner the keys of the write-only IAM application (secret files).
/// </para>
/// <para>
/// The pass reads its own keys from the same section (public host, SSR address, served epoch);
/// this class holds only what reaching the store takes.
/// </para>
/// </remarks>
public sealed class PageStoreOptions
{
    public const string SectionName = "LoDb:Prerender";

    /// <summary>Scaleway's name of the One Zone class.</summary>
    public const string OneZoneInfrequentAccess = "ONEZONE_IA";

    /// <summary>The class the development store (MinIO) takes in place of One Zone.</summary>
    public const string ReducedRedundancy = "REDUCED_REDUNDANCY";

    private const string DefaultRegion = "fr-par";
    private const int DefaultRequestTimeoutSeconds = 30;

    /// <summary>
    /// Base URL of the S3 API, scheme and authority alone (<c>https://s3.fr-par.scw.cloud</c>);
    /// objects are addressed path-style, <c>/{bucket}/{key}</c>.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Region of the signature scope.</summary>
    public string Region { get; set; } = DefaultRegion;

    /// <summary>The environment's bucket: <c>lodb-pages-prod</c>, <c>lodb-pages-staging</c>.
    /// </summary>
    public string? Bucket { get; set; }

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>
    /// Lets <see cref="Endpoint"/> be plain http: the development stack's store only, never a
    /// host.
    /// </summary>
    public bool AllowInsecureEndpoint { get; set; }

    /// <summary>
    /// The store's name for <see cref="PageStorageClass.OneZone"/>:
    /// <see cref="OneZoneInfrequentAccess"/> on Scaleway, <see cref="ReducedRedundancy"/> on
    /// MinIO, which refuses any other class. Anything but One Zone needs
    /// <see cref="AllowInsecureEndpoint"/>: a host never stores pages in a dearer class.
    /// </summary>
    public string OneZoneStorageClass { get; set; } = OneZoneInfrequentAccess;

    public TimeSpan RequestTimeout { get; set; } =
        TimeSpan.FromSeconds(DefaultRequestTimeoutSeconds);

    /// <summary>Whether the store has all it needs to be written to.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrEmpty(AccessKey)
        && !string.IsNullOrEmpty(SecretKey);
}
