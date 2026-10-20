using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LoDb.Infrastructure.PageStore;
using Xunit;

namespace LoDb.Testing;

/// <summary>
/// A throwaway S3 store (MinIO) with one empty bucket, standing in for Scaleway in the tests
/// of the page store and of the pass.
/// </summary>
/// <remarks>
/// <para>
/// A test and development simulator only, never a production dependency (ADR 0004 rejected
/// MinIO on the host): what Scaleway itself does (classes, anonymous reads, metadata on GET)
/// is proven by <c>tools/prerender</c>'s check against the real bucket, not by CI.
/// </para>
/// <para>
/// MinIO's own images stopped being published in 2025: the community fork's, pinned by digest,
/// started as a generic container so no MinIO package joins the solution. Its data lives in a
/// tmpfs and goes with the container. MinIO knows no One Zone class:
/// <see cref="Settings"/> maps it to <c>REDUCED_REDUNDANCY</c>, which it keeps and reports.
/// </para>
/// </remarks>
public sealed class PageStoreContainerFixture : IAsyncLifetime
{
    public const string Image =
        "pgsty/minio:RELEASE.2026-08-04T00-00-00Z"
        + "@sha256:b6bfe7239bfc83fb90d31612d9704d86039dd714f7904b3f1ad68f211e602372";

    /// <summary>The bucket every test writes to, each under keys of its own.</summary>
    public const string Bucket = "lodb-pages-test";

    /// <summary>The root account, the only one: it signs every request of the tests.</summary>
    public const string AccessKey = "lodb-test-writer";

    public const string SecretKey = "lodb-test-writer-secret";

    private const int ApiPort = 9000;
    private const string DataPath = "/data";
    private const string Alias = "local";
    private const string Section = PageStoreOptions.SectionName;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithCommand("server", DataPath)
        .WithEnvironment("MINIO_ROOT_USER", AccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
        .WithTmpfsMount(DataPath)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(static request =>
            request.ForPort(ApiPort).ForPath("/minio/health/live")))
        .Build();

    /// <summary>Base URL of the S3 API, plain http, from the host running the tests.</summary>
    public Uri Endpoint => new UriBuilder(
        Uri.UriSchemeHttp,
        _container.Hostname,
        _container.GetMappedPublicPort(ApiPort)).Uri;

    /// <summary>The settings (<c>LoDb:Prerender</c>) that point the page store here.</summary>
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        [Key(nameof(PageStoreOptions.Endpoint))] = Endpoint.GetLeftPart(UriPartial.Authority),
        [Key(nameof(PageStoreOptions.Bucket))] = Bucket,
        [Key(nameof(PageStoreOptions.AccessKey))] = AccessKey,
        [Key(nameof(PageStoreOptions.SecretKey))] = SecretKey,
        [Key(nameof(PageStoreOptions.AllowInsecureEndpoint))] = bool.TrueString,
        [Key(nameof(PageStoreOptions.OneZoneStorageClass))] = PageStoreOptions.ReducedRedundancy,
    };

    /// <summary>Starts the store, then creates the bucket with the image's own client.</summary>
    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _container.StartAsync(cancellationToken);
        var local = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{ApiPort}");
        string[] command =
        [
            "sh", "-c", "mc alias set \"$1\" \"$2\" \"$3\" \"$4\" && mc mb \"$1/$5\"",
            "bootstrap", Alias, local, AccessKey, SecretKey, Bucket,
        ];
        var result = await _container.ExecAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The bucket was not created: {result.Stderr}");
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static string Key(string setting) => $"{Section}:{setting}";
}
