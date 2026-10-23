using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The pages of the automatic queue, one state at a time, fifty a page: pending ones in the
/// worker's order, done ones the last written first; a state the queue has not, or a page below
/// one, refused.
/// </summary>
public sealed class AdminPrerenderDemandPagesTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Pages = "/api/admin/prerender/demand/pages";

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
    public async Task PendingPagesComeInTheWorkersOrderAlone()
    {
        await SeedAsync(
            Row("/fr/16.11.1/runes", PrerenderDemandState.Pending, 3, Now.AddHours(-2)),
            Row("/en/16.11.1/runes", PrerenderDemandState.Pending, 9, Now),
            Row("/de/16.11.1/runes", PrerenderDemandState.Pending, 3, Now.AddHours(-5)),
            Row("/es/16.11.1/runes", PrerenderDemandState.Failed, 20, Now));
        using var admin = await AdminBrowser.OpenAsync(App);

        var listing = await ReadAsync(admin, "?state=pending");

        Assert.Equal(
            ("pending", 1, 50, 3L),
            (ApiJson.Text(listing, "state"), listing.GetProperty("page").GetInt32(),
                listing.GetProperty("pageSize").GetInt32(), listing.GetProperty("total").GetInt64()));
        Assert.Equal(
            ["/en/16.11.1/runes", "/de/16.11.1/runes", "/fr/16.11.1/runes"],
            PathsOf(listing));
    }

    [Fact]
    public async Task DonePagesComeTheLastWrittenFirstWithWhatTheyHold()
    {
        var older = Row("/fr/16.11.1/runes", PrerenderDemandState.Done, 1, Now);
        (older.RenderedAt, older.Bytes, older.StoredClass) = (Now.AddHours(-3), 2048, "archived");
        var newer = Row("/en/16.11.1/runes", PrerenderDemandState.Done, 1, Now);
        (newer.RenderedAt, newer.StoredClass, newer.SupersededAt) =
            (Now.AddMinutes(-5), "revisable", Now);
        await SeedAsync(older, newer);
        using var admin = await AdminBrowser.OpenAsync(App);

        var listing = await ReadAsync(admin, "?state=done");

        Assert.Equal(["/en/16.11.1/runes", "/fr/16.11.1/runes"], PathsOf(listing));
        var first = listing.GetProperty("pages")[0];
        var second = listing.GetProperty("pages")[1];
        Assert.Equal(
            ("revisable", JsonValueKind.String, "runes", 1),
            (ApiJson.Text(first, "storedClass"), first.GetProperty("supersededAt").ValueKind,
                ApiJson.Text(first, "pageType"), first.GetProperty("epoch").GetInt32()));
        Assert.Equal(
            (2048L, JsonValueKind.Null),
            (second.GetProperty("bytes").GetInt64(), second.GetProperty("supersededAt").ValueKind));
    }

    [Fact]
    public async Task SecondPageHoldsWhatTheFirstLeaves()
    {
        await SeedAsync([.. Enumerable.Range(0, 51).Select(index => Row(
            string.Create(CultureInfo.InvariantCulture, $"/en/16.11.1/items/{1000 + index}"),
            PrerenderDemandState.Skipped,
            1,
            Now))]);
        using var admin = await AdminBrowser.OpenAsync(App);

        var first = await ReadAsync(admin, "?state=skipped");
        var second = await ReadAsync(admin, "?state=skipped&page=2");

        Assert.Equal((50, 51L), (PathsOf(first).Count, first.GetProperty("total").GetInt64()));
        var last = Assert.Single(PathsOf(second));
        Assert.DoesNotContain(last, PathsOf(first));
    }

    [Theory]
    [InlineData("", "state", "required")]
    [InlineData("?state=stopped", "state", "malformed")]
    [InlineData("?state=done&page=0", "page", "malformed")]
    public async Task UnknownStateOrPageIsRefused(string query, string field, string code)
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Pages + query);

        Assert.Equal([code], (await ApiJson.FieldErrorsAsync(response))[field]);
    }

    private static List<string?> PathsOf(JsonElement listing) =>
    [
        .. listing.GetProperty("pages").EnumerateArray()
            .Select(static page => page.GetProperty("path").GetString()),
    ];

    private static async Task<JsonElement> ReadAsync(BrowserClient browser, string query)
    {
        using var response = await browser.GetAsync(Pages + query);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private PrerenderDemandPage Row(
        string path,
        PrerenderDemandState state,
        long demand,
        DateTimeOffset firstSeenAt)
    {
        var parts = path.Split('/');
        return new PrerenderDemandPage
        {
            Path = path,
            Version = parts[2],
            Locale = parts[1],
            PageType = parts[3],
            Demand = demand,
            State = state,
            FirstSeenAt = firstSeenAt,
            LastSeenAt = Now,
            UpdatedAt = Now,
            Epoch = state == PrerenderDemandState.Pending ? null : 1,
        };
    }

    private async Task SeedAsync(params PrerenderDemandPage[] pages)
    {
        await using var db = App.Database();
        db.PrerenderDemand.AddRange(pages);
        await db.SaveChangesAsync(Cancellation);
    }
}
