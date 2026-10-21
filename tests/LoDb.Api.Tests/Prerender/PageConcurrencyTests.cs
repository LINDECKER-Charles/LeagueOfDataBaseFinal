using System.Globalization;
using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// The pages of a unit go <c>MaxInFlight</c> at a time; the cursor saved never passes a page
/// that did not end, and no page is counted twice.
/// </summary>
public sealed class PageConcurrencyTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.10.1");
    private static readonly string First = $"/en/{Version.Value}/champions";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PagesOfAUnitAreRenderedTwoAtATime()
    {
        await using var harness = await StartAsync(maxInFlight: 2);
        var (inFlight, most) = (0, 0);
        harness.Ssr.OnPage = async _ =>
        {
            most = Math.Max(most, Interlocked.Increment(ref inFlight));
            await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
            Interlocked.Decrement(ref inFlight);
        };
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.Equal(2, most);
        var pass = await harness.PassAsync(passId);
        var unit = Assert.Single(pass.Units);
        Assert.Equal((PrerenderPassState.Completed, 6L), (pass.State, pass.PagesWritten));
        Assert.Equal((6, 6), (unit.Cursor, unit.PagesWritten));
    }

    [Fact]
    public async Task PauseKeepsTheCursorOnTheFirstPageThatDidNotEnd()
    {
        await using var harness = await StartAsync(maxInFlight: 3);
        var passId = await harness.LaunchAsync(Scope());
        harness.Ssr.Answer(First, () => FakeSsr.Status(HttpStatusCode.ServiceUnavailable, 1));
        harness.Ssr.OnPage = async path =>
        {
            if (path == First)
            {
                await harness.Get<PassControl>().PauseAsync(passId, Token);
            }
        };

        await harness.RunAsync();
        var paused = Assert.Single((await harness.PassAsync(passId)).Units);
        harness.Ssr.OnPage = null;
        await harness.Get<PassControl>().ResumeAsync(passId, Token);
        await harness.RunAsync();

        Assert.Equal((0, 0), (paused.Cursor, paused.PagesWritten));
        var pass = await harness.PassAsync(passId);
        var unit = Assert.Single(pass.Units);
        Assert.Equal((PrerenderPassState.Completed, 6L), (pass.State, pass.PagesWritten));
        Assert.Equal((6, 6, 0), (unit.Cursor, unit.PagesWritten, unit.PagesSkipped));
        Assert.Equal(
            harness.Store!.Objects.Values
                .Where(static page => page.Key.StartsWith("pages/", StringComparison.Ordinal))
                .Sum(static page => (long)page.Body.Length),
            unit.BytesWritten);
    }

    private static PassScope Scope() => PassScope.Of(Version) with { Locales = [UiLocale.En] };

    private async Task<PrerenderHarness> StartAsync(int maxInFlight)
    {
        var harness = await PrerenderHarness.StartAsync(
            postgres,
            settings: new Dictionary<string, string?>
            {
                ["LoDb:Prerender:MaxInFlight"] =
                    maxInFlight.ToString(CultureInfo.InvariantCulture),
            });
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
