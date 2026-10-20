using System.Net;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Tests.PageStore.Support;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.PageStore;

/// <summary>
/// The store against a real S3 server (MinIO): what a PUT stores comes back on a HEAD, the
/// body untouched; a create-only write keeps the first object; an absent object is a value;
/// a request the server refuses never puts the keys in a log line nor in an exception.
/// </summary>
public sealed class MinioPageStoreTests(PageStoreContainerFixture minio)
    : IClassFixture<PageStoreContainerFixture>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APageComesBackWithItsHeadersMetadataAndClass()
    {
        await using var harness = new PageStoreHarness(minio.Settings);
        var metadata = new Dictionary<string, string>
        {
            ["content-security-policy"] =
                "script-src 'self' 'sha256-Zm9vYmFyYmF6cXV4'; object-src 'none'",
            ["content-security-policy-report-only"] = "require-trusted-types-for 'script'",
            ["reporting-endpoints"] = "csp=\"/api/csp-reports\"",
            ["link"] = "<https://ddragon.leagueoflegends.com>; rel=preconnect; crossorigin",
            ["lodb-build"] = "9f2c4e",
        };
        var page = StoredPages.Page(UniqueKey("pages/1/fr/16.1.1/items/1001-bottes"), metadata);

        var outcome = await harness.Store.PutAsync(page, Cancellation);
        var stored = await harness.Store.HeadAsync(page.Key, Cancellation);

        Assert.Equal(PageWriteOutcome.Written, outcome);
        Assert.NotNull(stored);
        Assert.Equal(page.Body.Length, stored.ContentLength);
        Assert.Equal(StoredPages.HtmlType, stored.ContentType);
        Assert.Equal("gzip", stored.ContentEncoding);
        Assert.Equal(StoredPages.ArchivedCacheControl, stored.CacheControl);
        Assert.Equal(PageStoreOptions.ReducedRedundancy, stored.StorageClass);
        Assert.Equal(metadata, stored.Metadata);
    }

    // A space, a plus and a dollar sign: the path sent and the path signed must agree.
    [Fact]
    public async Task AKeyWithCharactersToEscapeIsStoredUnderThatKey()
    {
        await using var harness = new PageStoreHarness(minio.Settings);
        var key = UniqueKey("pages/0/probe/a b+c$d");

        await harness.Store.PutAsync(StoredPages.BuildFile(key, "probe"), Cancellation);

        Assert.NotNull(await harness.Store.HeadAsync(key, Cancellation));
    }

    [Fact]
    public async Task ACreateOnlyWriteKeepsTheFirstObject()
    {
        await using var harness = new PageStoreHarness(minio.Settings);
        var key = UniqueKey("build/main-ABCD1234.js");

        var first = await harness.Store.PutAsync(
            StoredPages.BuildFile(key, "first"),
            Cancellation);
        var second = await harness.Store.PutAsync(
            StoredPages.BuildFile(key, "second, longer"),
            Cancellation);
        var stored = await harness.Store.HeadAsync(key, Cancellation);

        Assert.Equal(PageWriteOutcome.Written, first);
        Assert.Equal(PageWriteOutcome.AlreadyPresent, second);
        Assert.Equal("first".Length, stored!.ContentLength);
        Assert.Equal(StoredPage.StandardStorageClass, stored.StorageClass);
    }

    [Fact]
    public async Task AMissingObjectIsAbsent()
    {
        await using var harness = new PageStoreHarness(minio.Settings);

        Assert.Null(await harness.Store.HeadAsync(UniqueKey("pages/1/fr/none"), Cancellation));
    }

    // A wrong secret: the server answers 403, an absence for a HEAD, a configuration error for
    // a PUT.
    [Fact]
    public async Task ARefusedRequestNeverLeaksTheKeys()
    {
        const string WrongSecret = "wrong-secret-7d1e9b";
        var settings = new Dictionary<string, string?>(minio.Settings)
        {
            [$"{PageStoreOptions.SectionName}:{nameof(PageStoreOptions.SecretKey)}"] = WrongSecret,
        };
        await using var harness = new PageStoreHarness(settings);
        var page = StoredPages.Page(
            UniqueKey("pages/1/fr/refused"),
            new Dictionary<string, string>());

        var stored = await harness.Store.HeadAsync(page.Key, Cancellation);
        var refusal = await Assert.ThrowsAsync<PageStoreConfigurationException>(
            () => harness.Store.PutAsync(page, Cancellation));

        Assert.Null(stored);
        Assert.Equal(HttpStatusCode.Forbidden, refusal.StatusCode);
        Assert.Contains("SignatureDoesNotMatch", refusal.Message, StringComparison.Ordinal);
        string[] secrets = [WrongSecret, PageStoreContainerFixture.AccessKey];
        var written = harness.Logs
            .Select(static record => $"{record.Message} {record.Exception}")
            .Append(refusal.ToString());
        Assert.DoesNotContain(written, text =>
            secrets.Any(secret => text.Contains(secret, StringComparison.Ordinal)));
    }

    private static string UniqueKey(string key)
    {
        var slash = key.IndexOf('/', StringComparison.Ordinal);
        return $"{key[..slash]}/{Guid.NewGuid():N}{key[slash..]}";
    }
}
