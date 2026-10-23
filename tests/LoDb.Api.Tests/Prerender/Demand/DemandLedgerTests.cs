using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// What the queue holds in the store: a written page keeps its bytes and class until the
/// served epoch changes; a pass that writes it again in the same epoch takes it over, and only
/// there.
/// </summary>
public sealed class DemandLedgerTests(PostgresContainerFixture postgres)
{
    private static readonly string Aatrox = DemandRig.PathOf("champions/Aatrox");
    private static readonly string AatroxFr = DemandRig.PathOf("champions/Aatrox", "fr");
    private static readonly string Runes = DemandRig.PathOf("runes");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WrittenPageKeepsItsBytesAndItsClass()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Ssr.Answer(Runes, static () => FakeSsr.Revisable(Runes, FakeSsr.BuildA));
        rig.Refused.Refuse(Aatrox);
        rig.Refused.Refuse(Runes);
        await rig.CollectAsync();

        await rig.DrainAsync();

        var aatrox = await rig.RowAsync(Aatrox);
        var runes = await rig.RowAsync(Runes);
        Assert.Equal(
            ((long?)rig.Harness.Store!.Objects[$"pages/1{Aatrox}"].Body.Length, "archived"),
            (aatrox!.Bytes, aatrox.StoredClass));
        Assert.Equal("revisable", runes!.StoredClass);
        Assert.Null(aatrox.SupersededAt);
    }

    [Fact]
    public async Task PageOfAnEpochNoLongerServedLosesItsBytesAndItsClass()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();
        await rig.DrainAsync();

        await rig.Harness.Get<DemandQueue>().ReviveAsync(2, Token);

        var row = await rig.RowAsync(Aatrox);
        Assert.Equal(
            (PrerenderDemandState.Pending, (long?)null, (string?)null),
            (row!.State, row.Bytes, row.StoredClass));
    }

    [Fact]
    public async Task PassThatWritesAQueuePageAgainTakesItOverInItsEpochOnly()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        rig.Refused.Refuse(AatroxFr);
        await rig.CollectAsync();
        await rig.DrainAsync();
        var english = PassScope.Of(DemandRig.Version) with { Locales = [UiLocale.En] };
        var french = PassScope.Of(DemandRig.Version) with { Locales = [UiLocale.Fr] };

        await rig.Harness.OpenAsync(new PassLaunch { Epoch = 2, Scope = french });
        await rig.Harness.RunAsync();
        await rig.Harness.LaunchAsync(english);
        await rig.Harness.RunAsync();

        var rewritten = await rig.RowAsync(Aatrox);
        var kept = await rig.RowAsync(AatroxFr);
        Assert.Equal(PrerenderDemandState.Done, rewritten!.State);
        Assert.NotNull(rewritten.SupersededAt);
        Assert.Null(kept!.SupersededAt);
    }
}
