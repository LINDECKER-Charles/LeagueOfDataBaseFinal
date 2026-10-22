using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Revision.Queue;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender.Revision;

/// <summary>
/// The drain of the queue of the closed states: a page is rendered once due, into its row's
/// epoch, at the pass's key and with its metadata, whatever a pass stored there before; a
/// retired epoch is skipped, a failed try is due again later, and the drain stops on an SSR
/// out of reach.
/// </summary>
public sealed class RevisionDrainTests(PostgresContainerFixture postgres)
{
    private const string Page = "/en/16.9.1/items/1036-long-sword";

    private static readonly DateTimeOffset ClosedByPromotion = RevisionRig.BuiltAt.AddHours(1);

    private static readonly TimeSpan PastSettling = TimeSpan.FromMinutes(22);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DuePageIsWrittenAgainAtThePassKeyOverWhatAPassStored()
    {
        await using var rig = await StartAsync();
        await rig.Harness.LaunchAsync(PassScope.Of(RevisionRig.Older) with
        {
            Locales = [UiLocale.En],
        });
        await rig.Harness.RunAsync();
        var passRenders = rig.Harness.Ssr.Pages.Count(static page => page == Page);
        await EnqueueAsync(rig);

        await rig.DrainAsync();
        var early = rig.Harness.Ssr.Pages.Count(static page => page == Page);
        rig.Harness.Clock.Advance(PastSettling);
        await rig.DrainAsync();

        Assert.Equal((1, 1), (passRenders, early));
        Assert.Equal(2, rig.Harness.Ssr.Pages.Count(static page => page == Page));
        var written = rig.Harness.Store!.Objects[$"pages/1{Page}"];
        Assert.Equal(FakeSsr.BuildA, written.Metadata["lodb-build"]);
        var row = (await rig.RowsAsync()).Single(static row => row.Path == Page);
        Assert.Equal(PrerenderDemandState.Done, row.State);
        Assert.NotNull(row.WrittenAt);
        Assert.Contains(
            rig.Harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.revision.drained");
    }

    [Fact]
    public async Task PageOfAnEpochNoLongerServedIsSkipped()
    {
        await using var rig = await StartAsync([RevisionRig.Setting("ServedEpoch", 2)]);
        await EnqueueAsync(rig);
        await using (var db = rig.Harness.Database.CreateContext())
        {
            // Enqueued for epoch 1 before epoch 2 was served.
            await db.PrerenderRevisions.ExecuteUpdateAsync(
                static setters => setters.SetProperty(static row => row.Epoch, 1),
                Token);
        }

        rig.Harness.Clock.Advance(PastSettling);
        await rig.DrainAsync();

        Assert.Empty(rig.Harness.Ssr.Pages);
        Assert.All(
            await rig.RowsAsync(),
            static row => Assert.Equal(PrerenderDemandState.Skipped, row.State));
    }

    [Fact]
    public async Task FailedTryIsDueAgainLaterThenFailedForGood()
    {
        await using var rig = await StartAsync();
        Func<HttpResponseMessage> failing = static () => FakeSsr.Status(HttpStatusCode.BadGateway);
        rig.Harness.Ssr.Answer(Page, failing, failing, failing);
        await EnqueueAsync(rig, onlyEnglish: true);
        rig.Harness.Clock.Advance(PastSettling);
        var drainedAt = rig.Harness.Clock.GetUtcNow();

        await rig.DrainAsync();
        var afterOne = (await rig.RowsAsync()).Single(static row => row.Path == Page);
        for (var retry = 0; retry < 2; retry++)
        {
            rig.Harness.Clock.Advance(RevisionQueue.RetryAfter);
            await rig.DrainAsync();
        }

        Assert.Equal((PrerenderDemandState.Pending, 1), (afterOne.State, afterOne.Attempts));
        Assert.InRange(
            afterOne.DueAt,
            drainedAt + RevisionQueue.RetryAfter,
            drainedAt + RevisionQueue.RetryAfter + TimeSpan.FromMinutes(1));
        var row = (await rig.RowsAsync()).Single(static row => row.Path == Page);
        Assert.Equal((PrerenderDemandState.Failed, 3), (row.State, row.Attempts));
    }

    [Fact]
    public async Task SsrOutOfReachStopsTheDrainWithThePageStillPending()
    {
        await using var rig = await StartAsync();
        rig.Harness.Ssr.Answer(Page, static () => throw new HttpRequestException(
            HttpRequestError.ConnectionError,
            "refused"));
        await EnqueueAsync(rig, onlyEnglish: true);
        rig.Harness.Clock.Advance(PastSettling);

        await rig.DrainAsync();

        var row = (await rig.RowsAsync()).Single(static row => row.Path == Page);
        Assert.Equal((PrerenderDemandState.Pending, 0), (row.State, row.Attempts));
        Assert.Contains(
            rig.Harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.revision.stopped");
    }

    private Task<RevisionRig> StartAsync(
        IEnumerable<KeyValuePair<string, string?>>? settings = null) =>
        RevisionRig.StartAsync(postgres, settings?.ToDictionary());

    // The state of the item from 16.9.1 to 16.10.1, closed by the promotion of 16.11.1: its
    // pages of both versions in every locale, or in English alone.
    private static async Task EnqueueAsync(RevisionRig rig, bool onlyEnglish = false)
    {
        await rig.SettleAsync();
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));
        await rig.ProduceAsync();
        if (onlyEnglish)
        {
            await using var db = rig.Harness.Database.CreateContext();
            db.PrerenderRevisions.RemoveRange(db.PrerenderRevisions.Where(static row =>
                row.Locale != "en"));
            await db.SaveChangesAsync(Token);
        }
    }
}
