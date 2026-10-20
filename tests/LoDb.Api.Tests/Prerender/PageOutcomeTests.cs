using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// Only a 200 the SSR marked <c>archived</c>, gzip and named after a copied build is written;
/// the pass renders each page for the public host, and says what a unit did in one line.
/// </summary>
public sealed class PageOutcomeTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.10.1");

    private static readonly string[] Stored =
        ["summoners", "champions/Aatrox", "items/1036-long-sword"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RedirectsMissingPagesAndOtherClassesAreSkipped()
    {
        await using var harness = await StartAsync();
        harness.Ssr.Answer(
            PathOf("champions"),
            () => FakeSsr.Status(HttpStatusCode.MovedPermanently));
        harness.Ssr.Answer(PathOf("items"), () => FakeSsr.Status(HttpStatusCode.NotFound));
        harness.Ssr.Answer(
            PathOf("runes"),
            () => FakeSsr.Unmarked(PathOf("runes"), "public, max-age=0, s-maxage=1800"));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal((3, 3, 0), (unit.PagesWritten, unit.PagesSkipped, unit.PagesFailed));
        Assert.Equal(
            Stored.Select(static path => "pages/1" + PathOf(path)),
            harness.Store!.Writes.Where(static key => IsPage(key)));
        Assert.Equal(
            unit.BytesWritten,
            harness.Store.Objects.Values
                .Where(static page => IsPage(page.Key))
                .Sum(static page => (long)page.Body.Length));
    }

    [Fact]
    public async Task EveryPageIsAskedForThePublicHostOverHttpsInGzipAsThePass()
    {
        await using var harness = await StartAsync();
        await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.All(harness.Ssr.Requests, static request =>
        {
            Assert.Equal(PrerenderHarness.PublicHost, request.Headers["X-Forwarded-Host"]);
            Assert.Equal("https", request.Headers["X-Forwarded-Proto"]);
            Assert.Equal("gzip", request.Headers["Accept-Encoding"]);
            Assert.Equal("1", request.Headers["X-LoDb-Prerender"]);
        });
    }

    [Fact]
    public async Task BuildIsCopiedBeforeItsFirstPageAndOnlyOnce()
    {
        await using var harness = await StartAsync();
        var other = PatchVersion.Parse("16.9.1");
        harness.Catalog.Pinned.Add(other);
        await harness.LaunchAsync(new PassScope { Locale = UiLocale.En });

        await harness.RunAsync();

        Assert.Equal(
            ["build/main-ABCD1234.js", "build/media/font-ABCD1234.woff2"],
            harness.Store!.Writes.Take(2));
        Assert.Equal(2, harness.Store.Writes.Count(static key => IsBuildFile(key)));
        Assert.Single(
            harness.Ssr.Requests,
            static request => request.Path == FakeSsr.BuildFilesPath);
        await using var db = harness.Database.CreateContext();
        var build = await db.PrerenderBuilds.SingleAsync(Token);
        Assert.Equal((FakeSsr.BuildA, 2), (build.Hash, build.Files));
        Assert.Equal(
            harness.Store.Objects.Values
                .Where(static file => IsBuildFile(file.Key))
                .Sum(static file => (long)file.Body.Length),
            build.Bytes);
    }

    [Fact]
    public async Task BuildCopiedOverTwoTriesCountsEveryFileItWrote()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("build/media/", new PageStoreTransientException(
            "PUT build/media/font-ABCD1234.woff2 answered 503 (SlowDown).",
            HttpStatusCode.ServiceUnavailable));
        await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        await using var db = harness.Database.CreateContext();
        var build = await db.PrerenderBuilds.SingleAsync(Token);
        var files = harness.Store.Objects.Values.Where(static file => IsBuildFile(file.Key));
        Assert.Equal(
            (2, files.Sum(static file => (long)file.Body.Length)),
            (build.Files, build.Bytes));
    }

    [Fact]
    public async Task PageOfABuildTheSsrLeftIsRenderedAgainUnderTheNewOne()
    {
        await using var harness = await StartAsync();
        harness.Ssr.Build = FakeSsr.BuildB;
        var first = PathOf("champions");
        harness.Ssr.Answer(first, () => FakeSsr.Archived(first, FakeSsr.BuildA));
        await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.Equal(2, harness.Ssr.Pages.Count(path => path == first));
        var written = harness.Store!.Objects["pages/1" + first];
        Assert.Equal(FakeSsr.BuildB, written.Metadata["lodb-build"]);
        await using var db = harness.Database.CreateContext();
        Assert.Equal(
            [FakeSsr.BuildB],
            await db.PrerenderBuilds.Select(static row => row.Hash).ToListAsync(Token));
    }

    [Fact]
    public async Task EachUnitSaysWhatItDidInOneLine()
    {
        await using var harness = await StartAsync();
        await harness.LaunchAsync(PassScope.Of(Version) with { Locale = null });

        await harness.RunAsync();

        var lines = harness.Logs.GetSnapshot()
            .Where(static record =>
                record.Category?.Contains("Prerender", StringComparison.Ordinal) == true)
            .ToList();
        var summaries = lines
            .Where(static record => record.Id.Name == "prerender.unit.completed")
            .ToList();
        Assert.Equal(UiLocales.All.Count, summaries.Count);
        Assert.Equal(summaries.Count + 1, lines.Count);
        Assert.Contains(lines, static record => record.Id.Name == "prerender.pass.completed");
        Assert.Contains("6 pages written", summaries[0].Message, StringComparison.Ordinal);
    }

    private static string PathOf(string page) => $"/en/{Version.Value}/{page}";

    private static bool IsPage(string key) => key.StartsWith("pages/", StringComparison.Ordinal);

    private static bool IsBuildFile(string key) =>
        key.StartsWith("build/", StringComparison.Ordinal);

    private static PassScope Scope() => PassScope.Of(Version) with { Locale = UiLocale.En };

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
