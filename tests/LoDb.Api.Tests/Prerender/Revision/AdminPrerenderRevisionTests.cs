using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Revision;

/// <summary>
/// The queue of the closed states as the admin reads it: its counts by state and by epoch, the
/// pages due now and the next one, the pages written lately, how far its producer went; to an
/// administrator alone.
/// </summary>
public sealed class AdminPrerenderRevisionTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Revision = "/api/admin/prerender/revision";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string Sword = "/16.9.1/items/1036-long-sword";

    private static readonly DateTimeOffset ClosedThrough =
        new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReportCountsTheQueueByEpochAndSaysWhatIsDue()
    {
        await SeedAsync(
            Page("/fr" + Sword, 1, PrerenderDemandState.Pending, Now.AddMinutes(-1)),
            Page("/en" + Sword, 1, PrerenderDemandState.Pending, Now.AddMinutes(20)),
            Page("/de" + Sword, 1, PrerenderDemandState.Done, Now.AddHours(-2)),
            Page("/de" + Sword, 2, PrerenderDemandState.Failed, Now.AddHours(-2)));
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Revision);

        var report = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(
            (2L, 1L, 1L, 1L, 0L, 1L),
            (report.GetProperty("pending").GetInt64(), report.GetProperty("due").GetInt64(),
                report.GetProperty("done").GetInt64(), report.GetProperty("failed").GetInt64(),
                report.GetProperty("writtenLastHour").GetInt64(),
                report.GetProperty("writtenLastDay").GetInt64()));
        Assert.Equal(
            [(2, 0L, 1L), (1, 2L, 0L)],
            report.GetProperty("epochs").EnumerateArray().Select(static epoch => (
                epoch.GetProperty("epoch").GetInt32(),
                epoch.GetProperty("pending").GetInt64(),
                epoch.GetProperty("failed").GetInt64())));
        Assert.True(report.GetProperty("nextDueAt").GetDateTimeOffset() < Now);
        Assert.Equal(ClosedThrough, report.GetProperty("closedThrough").GetDateTimeOffset());
    }

    [Fact]
    public async Task OnlyAnAdministratorWithSecondFactorReadsIt()
    {
        using var visitor = App.Browser();
        using var unenrolled = await AdminCalls.OpenUnenrolledAsync(App);

        using var refused = await visitor.GetAsync(Revision);
        using var withoutFactor = await unenrolled.GetAsync(Revision);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "mfa-required",
            await ApiJson.ProblemCodeAsync(withoutFactor, HttpStatusCode.Forbidden));
    }

    // A pending page waits for its due time; an ended one was ended at that time.
    private static PrerenderRevisionPage Page(
        string path,
        int epoch,
        PrerenderDemandState state,
        DateTimeOffset at)
    {
        var parts = path.Split('/');
        return new PrerenderRevisionPage
        {
            Path = path,
            Epoch = epoch,
            Version = parts[2],
            Locale = parts[1],
            State = state,
            EnqueuedAt = Now.AddHours(-3),
            DueAt = state == PrerenderDemandState.Pending ? at : Now.AddHours(-3),
            WrittenAt = state == PrerenderDemandState.Done ? at : null,
            UpdatedAt = at,
        };
    }

    private async Task SeedAsync(params PrerenderRevisionPage[] pages)
    {
        await using var db = App.Database();
        db.PrerenderRevisions.AddRange(pages);
        db.PrerenderRevisionWatermarks.Add(new PrerenderRevisionWatermark
        {
            GenerationId = 1,
            ClosedThrough = ClosedThrough,
        });
        await db.SaveChangesAsync(Cancellation);
    }
}
