using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The queue writes its pages as a pass does, into the served epoch, the most requested first;
/// it waits while it should, gives up on a page that keeps failing, shares the SSR with a
/// running pass, and does nothing while switched off.
/// </summary>
public sealed class DemandDrainTests(PostgresContainerFixture postgres)
{
    private static readonly string Aatrox = DemandRig.PathOf("champions/Aatrox");
    private static readonly string Sword = DemandRig.PathOf("items/1036-long-sword");
    private static readonly string Runes = DemandRig.PathOf("runes");

    [Fact]
    public async Task PageIsWrittenAtThePassKeyWithThePassMetadata()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Sword);
        await rig.CollectAsync();

        await rig.DrainAsync();

        var written = rig.Harness.Store!.Objects[$"pages/1{Sword}"];
        Assert.Equal(FakeSsr.BodyOf(Sword), written.Body);
        Assert.Equal(("gzip", FakeSsr.ArchivedCaching), (written.ContentEncoding,
            written.CacheControl));
        Assert.Equal(FakeSsr.BuildA, written.Metadata["lodb-build"]);
        Assert.Equal(FakeSsr.Policy, written.Metadata["content-security-policy"]);
        Assert.Contains("build/main-ABCD1234.js", rig.Harness.Store.Writes);
        var row = await rig.RowAsync(Sword);
        Assert.Equal((PrerenderDemandState.Done, (int?)1), (row!.State, row.Epoch));
        Assert.NotNull(row.RenderedAt);
        Assert.Equal("1", rig.Harness.Ssr.Requests.Last().Headers["X-LoDb-Prerender"]);
    }

    [Fact]
    public async Task MostRequestedPagesGoFirst()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox, 2);
        rig.Refused.Refuse(Sword, 9);
        rig.Refused.Refuse(Runes, 5);
        await rig.CollectAsync();

        await rig.DrainAsync();

        Assert.Equal([Sword, Runes, Aatrox], rig.Harness.Ssr.Pages);
    }

    [Fact]
    public async Task PageDoneIsNotRenderedAgainWhileItsEpochIsServed()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();
        await rig.DrainAsync();

        rig.Refused.Refuse(Aatrox, 4);
        await rig.CollectAsync();
        await rig.DrainAsync();

        Assert.Single(rig.Harness.Ssr.Pages);
        var row = await rig.RowAsync(Aatrox);
        Assert.Equal((PrerenderDemandState.Done, 5L), (row!.State, row.Demand));
    }

    [Fact]
    public async Task PageAPassWroteSinceItEnteredIsSkipped()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();
        await rig.Harness.LaunchAsync(PassScope.Of(DemandRig.Version) with
        {
            Locales = [UiLocale.En],
        });
        await rig.Harness.RunAsync();
        var passPages = rig.Harness.Ssr.Pages.Count;

        await rig.DrainAsync();

        Assert.Equal(passPages, rig.Harness.Ssr.Pages.Count);
        Assert.Equal(PrerenderDemandState.Skipped, (await rig.RowAsync(Aatrox))!.State);
    }

    [Fact]
    public async Task VersionStillHeldForTheLatestWaitsWithoutATry()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Catalog.PinnedFrom[DemandRig.Version] = DateTimeOffset.UtcNow.AddHours(1);
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();

        await rig.DrainAsync();

        Assert.Empty(rig.Harness.Ssr.Pages);
        var row = await rig.RowAsync(Aatrox);
        Assert.Equal((PrerenderDemandState.Pending, 0), (row!.State, row.Attempts));
        Assert.StartsWith(
            "Its version just left the latest rank",
            row.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PageThatKeepsFailingIsFailedForGoodAtItsLastTry()
    {
        await using var rig = await DemandRig.StartAsync(
            postgres,
            new Dictionary<string, string?> { [$"{DemandOptions.SectionName}:MaxAttempts"] = "2" });
        rig.Harness.Ssr.Answer(
            Aatrox,
            () => FakeSsr.Status(HttpStatusCode.BadGateway),
            () => FakeSsr.Status(HttpStatusCode.BadGateway));
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();

        await rig.DrainAsync();
        var afterOne = await rig.RowAsync(Aatrox);
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(6));
        await rig.DrainAsync();

        Assert.Equal((PrerenderDemandState.Pending, 1), (afterOne!.State, afterOne.Attempts));
        var row = await rig.RowAsync(Aatrox);
        Assert.Equal((PrerenderDemandState.Failed, 2), (row!.State, row.Attempts));
        Assert.Equal("The SSR answered 502, not a page to store.", row.Reason);
    }

    [Fact]
    public async Task RefusalOfThePassSsrIsWaitedOutUncounted()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Ssr.Answer(Aatrox, () => FakeSsr.Status(HttpStatusCode.ServiceUnavailable, 7));
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();

        await rig.DrainAsync();

        Assert.Contains(TimeSpan.FromSeconds(7), rig.Harness.Clock.Waits);
        var row = await rig.RowAsync(Aatrox);
        Assert.Equal((PrerenderDemandState.Done, 0), (row!.State, row.Attempts));
    }

    [Fact]
    public async Task SsrOutOfReachStopsTheDrainWithThePageStillPending()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Ssr.Answer(Aatrox, static () => throw new HttpRequestException(
            HttpRequestError.ConnectionError,
            "refused"));
        rig.Refused.Refuse(Aatrox, 3);
        rig.Refused.Refuse(Sword);
        await rig.CollectAsync();

        await rig.DrainAsync();

        var row = await rig.RowAsync(Aatrox);
        Assert.Equal((PrerenderDemandState.Pending, 0), (row!.State, row.Attempts));
        Assert.DoesNotContain(Sword, rig.Harness.Ssr.Pages);
    }

    [Fact]
    public async Task SwitchedOffQueueRendersNothing()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();
        await using var off = await DemandRig.StartAsync(
            postgres,
            new Dictionary<string, string?> { [$"{DemandOptions.SectionName}:Enabled"] = "false" });

        await off.Harness.Get<DemandDrain>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Empty(off.Harness.Ssr.Pages);
        Assert.False(off.Harness.Get<DemandActivation>().IsActive);
    }

    [Fact]
    public async Task QueueAndPassShareTheSsrWithinItsSlots()
    {
        await using var rig = await DemandRig.StartAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Prerender:MaxInFlight"] = "2" });
        var (inFlight, most) = (0, 0);
        rig.Harness.Ssr.OnPage = async _ =>
        {
            most = Math.Max(most, Interlocked.Increment(ref inFlight));
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
            Interlocked.Decrement(ref inFlight);
        };
        foreach (var locale in new[] { "fr", "de", "es" })
        {
            rig.Refused.Refuse(DemandRig.PathOf("champions/Aatrox", locale));
        }

        await rig.CollectAsync();
        await rig.Harness.LaunchAsync(PassScope.Of(DemandRig.Version) with
        {
            Locales = [UiLocale.En],
        });

        await Task.WhenAll(rig.Harness.RunAsync(), rig.DrainAsync());

        Assert.InRange(most, 1, 2);
        Assert.Equal(6 + 3, rig.Harness.Ssr.Pages.Count);
        Assert.All(await rig.RowsAsync(), static row =>
            Assert.Equal(PrerenderDemandState.Done, row.State));
    }
}
