using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// What the pass leaves in a real S3 store (MinIO): a page as the SSR sent it, with the
/// headers nginx sets again from its metadata, and the build that rendered it beside it.
/// </summary>
public sealed class PageWritingTests(
    PostgresContainerFixture postgres,
    PageStoreContainerFixture store) : IClassFixture<PageStoreContainerFixture>
{
    private const string Entity = "items/1036-long-sword";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ArchivedPageIsStoredAsSentWithItsOwnHeadersAsMetadata()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres, store.Settings);
        var version = PatchVersion.Parse("16.11.1");
        harness.Catalog.Pinned.Add(version);
        await harness.LaunchAsync(Scope(version));

        await harness.RunAsync();

        var path = $"/en/{version.Value}/{Entity}";
        var stored = await harness.Get<IPageStore>().HeadAsync($"pages/1{path}", Token);
        Assert.NotNull(stored);
        Assert.Equal(FakeSsr.BodyOf(path).Length, stored.ContentLength);
        Assert.Equal(
            ("text/html; charset=utf-8", "gzip", FakeSsr.ArchivedCaching),
            (stored.ContentType, stored.ContentEncoding, stored.CacheControl));
        Assert.Equal(PageStoreOptions.ReducedRedundancy, stored.StorageClass);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["content-security-policy"] = FakeSsr.Policy,
                ["content-security-policy-report-only"] = FakeSsr.ReportOnlyPolicy,
                ["reporting-endpoints"] = FakeSsr.Endpoints,
                ["link"] = FakeSsr.Preconnect,
                ["lodb-build"] = FakeSsr.BuildA,
            },
            stored.Metadata);
    }

    [Fact]
    public async Task BuildFilesAreStoredForAYearInEveryZone()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres, store.Settings);
        var version = PatchVersion.Parse("16.12.1");
        harness.Catalog.Pinned.Add(version);
        await harness.LaunchAsync(Scope(version));

        await harness.RunAsync();

        var bundle = await harness.Get<IPageStore>().HeadAsync("build/main-ABCD1234.js", Token);
        var media = await harness.Get<IPageStore>().HeadAsync(
            "build/media/font-ABCD1234.woff2",
            Token);
        Assert.NotNull(bundle);
        Assert.NotNull(media);
        Assert.Equal(StoredPage.StandardStorageClass, bundle.StorageClass);
        Assert.Equal("public, max-age=31536000, immutable", bundle.CacheControl);
        Assert.Equal("text/javascript; charset=UTF-8", bundle.ContentType);
    }

    private static PassScope Scope(PatchVersion version) =>
        PassScope.Of(version) with
        {
            Locales = [UiLocale.En],
            PageTypes = [ResourceType.Items],
        };
}
