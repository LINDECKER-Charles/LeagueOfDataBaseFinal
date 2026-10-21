using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Units.Waits;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The progress of a pass for the live monitor: each version's cells in the order the worker
/// reaches them, what runs, what comes next, what ended and what failed; whole, or only the
/// versions changed since a recent read.
/// </summary>
/// <remarks>
/// A host per test, its pass written straight into the database: two versions in English and
/// French, one done with a failed page in French, the next one running in English.
/// </remarks>
public sealed class AdminPrerenderProgressTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Passes = "/api/admin/prerender/passes";
    private const string Done = "16.18.1";
    private const string Running = "16.17.1";

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private DateTimeOffset Now => App.Clock.GetUtcNow();

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
    public async Task CompleteReadShowsEveryVersionAndTheListOfTheScope()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync(PrerenderPassState.Running);

        var progress = await ReadAsync(admin, id, since: null);

        Assert.True(progress.GetProperty("isComplete").GetBoolean());
        Assert.Equal(["en", "fr"], Strings(progress.GetProperty("locales")));
        Assert.Equal([Done, Running], Strings(progress.GetProperty("scopeVersions")));
        Assert.Equal(
            [(Done, 0, "cw", 1000L), (Running, 2, "r.", 500L)],
            progress.GetProperty("versions").EnumerateArray().Select(static version =>
                (ApiJson.Text(version, "version")!, version.GetProperty("rank").GetInt32(),
                    ApiJson.Text(version, "cells")!,
                    version.GetProperty("pagesTotal").GetInt64())));
        Assert.Equal(id, progress.GetProperty("pass").GetProperty("id").GetInt64());
        Assert.Equal([Running + " en"], Keys(progress.GetProperty("running")));
        Assert.Equal([Running + " fr"], Keys(progress.GetProperty("next")));
        Assert.Equal([Done + " fr", Done + " en"], Keys(progress.GetProperty("recent")));
        var failure = Assert.Single(progress.GetProperty("failures").EnumerateArray());
        Assert.Equal(
            ("16.18.1", "fr", "The SSR answered 500, not a page to store."),
            (ApiJson.Text(failure, "version"), ApiJson.Text(failure, "locale"),
                ApiJson.Text(failure, "failure")));
        Assert.Equal(
            Now.AddMinutes(-15),
            failure.GetProperty("endedAt").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, progress.GetProperty("unitsWithFailures").GetInt32());
        var running = progress.GetProperty("running")[0];
        Assert.Equal(JsonValueKind.Null, running.GetProperty("endedAt").ValueKind);
        Assert.Equal(JsonValueKind.String, running.GetProperty("startedAt").ValueKind);
        var pulse = progress.GetProperty("pulse");
        Assert.False(pulse.GetProperty("isWorkerEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, pulse.GetProperty("observedAt").ValueKind);
    }

    [Fact]
    public async Task RecentReadShowsOnlyTheVersionsChangedSince()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync(PrerenderPassState.Running);

        var recent = await ReadAsync(admin, id, Now.AddMinutes(-5));
        var old = await ReadAsync(admin, id, Now.AddMinutes(-11));

        Assert.False(recent.GetProperty("isComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, recent.GetProperty("scopeVersions").ValueKind);
        Assert.Equal(
            [Running],
            recent.GetProperty("versions").EnumerateArray()
                .Select(static version => ApiJson.Text(version, "version")));
        Assert.True(old.GetProperty("isComplete").GetBoolean());
        Assert.Equal(2, old.GetProperty("versions").GetArrayLength());
    }

    [Theory]
    [InlineData(PrerenderPassState.Paused, "z.")]
    [InlineData(PrerenderPassState.Cancelled, "i.")]
    public async Task UnitLeftRunningIsStoppedOrInterruptedByThePass(
        PrerenderPassState state,
        string cells)
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync(state);

        var progress = await ReadAsync(admin, id, since: null);

        Assert.Equal(cells, ApiJson.Text(progress.GetProperty("versions")[1], "cells"));
    }

    // The worker takes a waiting version later: what comes next leaves it out, and the pulse
    // says why it waits and until when.
    [Fact]
    public async Task VersionWaitingOnThisInstanceIsLeftOutOfWhatComesNext()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync(PrerenderPassState.Running);
        var since = App.Clock.GetUtcNow();
        App.Services.GetRequiredService<UnitWaits>().TryBegin(
            new ClaimedUnit
            {
                Pass = new RunningPass(id, 1, PassScope.Everything),
                Version = PatchVersion.Parse(Running),
                Locale = UiLocale.Fr,
            },
            UnitWaitReason.CatalogPending);

        var progress = await ReadAsync(admin, id, since: null);

        Assert.Empty(progress.GetProperty("next").EnumerateArray());
        var wait = Assert.Single(progress.GetProperty("pulse").GetProperty("waiting")
            .EnumerateArray());
        Assert.Equal((Running, "catalog-pending"), (ApiJson.Text(wait, "version"),
            ApiJson.Text(wait, "reason")));
        Assert.Equal(
            wait.GetProperty("since").GetDateTimeOffset() + UnitWaits.Longest,
            wait.GetProperty("givesUpAt").GetDateTimeOffset());
        Assert.Equal(since, wait.GetProperty("since").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UnknownPassIsNotFoundAndAVisitorIsTurnedAway()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var visitor = App.Browser();

        using var unknown = await admin.GetAsync($"{Passes}/404/progress");
        using var refused = await visitor.GetAsync($"{Passes}/404/progress");

        Assert.Equal(
            "prerender-pass-not-found",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.NotFound));
        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Unauthorized));
    }

    private static async Task<JsonElement> ReadAsync(
        BrowserClient browser,
        long id,
        DateTimeOffset? since)
    {
        var query = since is { } from
            ? "?since=" + Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))
            : string.Empty;
        using var response = await browser.GetAsync($"{Passes}/{id}/progress{query}");
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private static List<string?> Strings(JsonElement array) =>
        [.. array.EnumerateArray().Select(static item => item.GetString())];

    private static List<string> Keys(JsonElement units) =>
    [
        .. units.EnumerateArray().Select(static unit =>
            $"{ApiJson.Text(unit, "version")} {ApiJson.Text(unit, "locale")}"),
    ];

    // 16.18.1 done 20 and 15 minutes ago, French with two failed pages; 16.17.1 running in
    // English for a minute, its French unit pending. Ranked in this order.
    private async Task<long> SeedAsync(PrerenderPassState state)
    {
        var isRunning = state == PrerenderPassState.Running;
        var isOver = state is PrerenderPassState.Completed or PrerenderPassState.Cancelled;
        var pass = new PrerenderPass
        {
            Epoch = 1,
            State = state,
            CreatedAt = Now.AddHours(-1),
            RunningSince = isRunning ? Now.AddMinutes(-30) : null,
            FinishedAt = isOver ? Now : null,
            Scope = $"{{\"versions\":[\"{Done}\",\"{Running}\"],\"locales\":[\"en\",\"fr\"]}}",
            Units =
            [
                Unit($"{Done} en", PrerenderUnitState.Completed, Now.AddMinutes(-20)),
                Unit($"{Done} fr", PrerenderUnitState.Completed, Now.AddMinutes(-15)),
                Unit($"{Running} en", PrerenderUnitState.Running, Now.AddMinutes(-1)),
                Unit($"{Running} fr", PrerenderUnitState.Pending, Now.AddHours(-1)),
            ],
        };
        pass.Units[1].PagesFailed = 2;
        pass.Units[1].Failure = "The SSR answered 500, not a page to store.";
        foreach (var (unit, rank) in pass.Units.Select(static (unit, rank) => (unit, rank)))
        {
            unit.Priority = rank;
        }

        await using var db = App.Database();
        db.PrerenderPasses.Add(pass);
        await db.SaveChangesAsync(Cancellation);
        return pass.Id;
    }

    // A unit of "version locale".
    private static PrerenderUnit Unit(string key, PrerenderUnitState state, DateTimeOffset at)
    {
        var (version, locale) = (key.Split(' ')[0], key.Split(' ')[1]);
        return new PrerenderUnit
        {
            Version = version,
            Locale = locale,
            State = state,
            PagesTotal = state == PrerenderUnitState.Pending ? 0 : 500,
            PagesWritten = state == PrerenderUnitState.Completed ? 498 : 0,
            StartedAt = state == PrerenderUnitState.Pending ? null : at.AddMinutes(-5),
            UpdatedAt = at,
            EndedAt = state == PrerenderUnitState.Completed ? at : null,
        };
    }
}
