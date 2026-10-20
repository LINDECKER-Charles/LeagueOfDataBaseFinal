using System.Net;
using LoDb.Api.Cli;
using LoDb.Api.Cli.Prerender;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// <c>prerender unit</c> writes one (version, locale) at once, under the worker's lock, as
/// the local check and the end-to-end setup run it: it waits a while for a catalog on its way,
/// and leaves no pass of its own active behind it.
/// </summary>
public sealed class PrerenderUnitCommandTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.5.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void ArgumentsDefaultToEnglishAndTheServedEpoch()
    {
        var options = PrerenderUnitArguments.Parse(
            ["--version", "16.5.1", "--ConnectionStrings:LoDb=Host=elsewhere"]);

        var scope = options.Scope;
        Assert.Equal(
            (Version, Version, Version, UiLocale.En, (ResourceType?)null),
            (options.Version, scope.Oldest, scope.Newest, scope.Locale, scope.PageType));
        Assert.Null(options.Epoch);
        Assert.Equal(PrerenderUnitArguments.DefaultWait, options.Wait);
    }

    [Fact]
    public void ArgumentsNameTheLocaleTheTypeAndTheEpoch()
    {
        var options = PrerenderUnitArguments.Parse(
            [
                "--version=16.5.1", "--locale", "zh-hant", "--type", "runes", "--epoch=2",
                "--wait=0",
            ]);

        Assert.Equal(
            (UiLocale.ZhHant, ResourceType.Runes, 2, TimeSpan.Zero),
            (options.Scope.Locale, options.Scope.PageType, options.Epoch, options.Wait));
    }

    [Theory]
    [InlineData("--locale", "fr")]
    [InlineData("--version", "latest")]
    [InlineData("--version", "16.5.1", "--type", "skins")]
    [InlineData("--version", "16.5.1", "--epoch", "0")]
    [InlineData("--version", "16.5.1", "--force")]
    [InlineData("--version", "16.5.1", "--wait", "soon")]
    public void InvalidUseIsRefused(params string[] arguments)
    {
        Assert.Throws<FormatException>(() => PrerenderUnitArguments.Parse(arguments));
    }

    [Fact]
    public async Task CommandWritesTheUnitThenCompletesItsPass()
    {
        await using var harness = await StartAsync();
        var command = new PrerenderUnitCommand(harness.Services);

        var exitCode = await command.ExecuteAsync(
            ["--version", Version.Value, "--locale", "fr", "--type", "champions"],
            Token);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(
            [$"/fr/{Version.Value}/champions", $"/fr/{Version.Value}/champions/Aatrox"],
            harness.Ssr.Pages);
        await using var db = harness.Database.CreateContext();
        var pass = await db.PrerenderPasses.SingleAsync(Token);
        Assert.Equal((PrerenderPassState.Completed, 2L), (pass.State, pass.PagesWritten));
    }

    [Fact]
    public async Task CommandIngestsTheCatalogThatItsHostWouldOnlyQueue()
    {
        await using var harness = await StartAsync();
        var command = new PrerenderUnitCommand(harness.Services);

        await command.ExecuteAsync(["--version", Version.Value, "--type", "runes"], Token);

        Assert.Equal([Version], harness.Catalog.Ingested);
    }

    [Fact]
    public async Task CommandIsRefusedWhileAnotherPassIsActive()
    {
        await using var harness = await StartAsync();
        await harness.LaunchAsync(PassScope.Everything);
        var command = new PrerenderUnitCommand(harness.Services);

        var exitCode = await command.ExecuteAsync(["--version", Version.Value], Token);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Empty(harness.Ssr.Requests);
    }

    [Fact]
    public async Task CatalogQueuedForIngestionIsWaitedFor()
    {
        await using var harness = await StartAsync();
        harness.Catalog.PendingReads[Version] = 2;
        var command = new PrerenderUnitCommand(harness.Services);

        var exitCode = await command.ExecuteAsync(["--version", Version.Value], Token);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(6, harness.Ssr.Pages.Count);
        Assert.Contains(TimeSpan.FromSeconds(5), harness.Clock.Waits);
    }

    [Fact]
    public async Task CatalogNotStoredInTimeCancelsTheCommandsPass()
    {
        await using var harness = await StartAsync();
        harness.Catalog.Pages[Version] = FakeCatalog.Without(CatalogLoadStatus.Pending);
        var command = new PrerenderUnitCommand(harness.Services);

        var exitCode = await command.ExecuteAsync(["--version", Version.Value, "--wait=30"], Token);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Equal(PrerenderPassState.Cancelled, (await SinglePassAsync(harness)).State);
        Assert.Empty(harness.Ssr.Requests);
    }

    [Fact]
    public async Task StoreRefusingTheWriterCancelsTheCommandsPassAndFreesTheNextRun()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("pages/", new PageStoreConfigurationException(
            "PUT /lodb-pages-test/pages/1/en/16.5.1/champions answered 403 (AccessDenied).",
            HttpStatusCode.Forbidden));
        var command = new PrerenderUnitCommand(harness.Services);

        var refused = await command.ExecuteAsync(["--version", Version.Value], Token);
        var cancelled = await SinglePassAsync(harness);
        var again = await command.ExecuteAsync(["--version", Version.Value], Token);

        Assert.Equal((CliExitCodes.Failure, CliExitCodes.Success), (refused, again));
        Assert.Equal(PrerenderPassState.Cancelled, cancelled.State);
    }

    [Fact]
    public async Task VersionThatIsNotAnOlderPatchFails()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        var command = new PrerenderUnitCommand(harness.Services);

        var exitCode = await command.ExecuteAsync(["--version", Version.Value], Token);

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    private static async Task<PrerenderPass> SinglePassAsync(PrerenderHarness harness)
    {
        await using var db = harness.Database.CreateContext();
        return await db.PrerenderPasses.AsNoTracking().SingleAsync(Token);
    }

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
