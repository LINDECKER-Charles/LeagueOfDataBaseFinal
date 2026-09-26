using System.Globalization;
using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class RedirectTests
{
    private const string Origin = "https://ddragon.leagueoflegends.com";

    [Theory]
    [InlineData("https://evil.example.com/steal")]
    [InlineData("http://ddragon.leagueoflegends.com/downgraded")]
    public async Task RedirectOutsideTheAllowListIsRefused(string target)
    {
        var upstream = FakeUpstream.Answering(_ => FakeUpstream.Redirect(target));
        using var harness = EgressHarness.Create(upstream);

        var refusal = await Assert.ThrowsAsync<EgressRefusedException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.Equal(EgressRefusal.Redirect, refusal.Reason);
        Assert.Equal(1, upstream.Calls);
    }

    [Fact]
    public async Task PermittedRedirectChainIsFollowed()
    {
        var upstream = FakeUpstream.Answering(request => request.RequestUri!.AbsolutePath switch
        {
            "/start" => FakeUpstream.Redirect("https://raw.communitydragon.org/hop"),
            "/hop" => FakeUpstream.Redirect("/relative"),
            _ => FakeUpstream.Response(HttpStatusCode.OK, "final"),
        });
        using var harness = EgressHarness.Create(upstream);

        var outcome = await harness.FetchAsync(Origin + "/start");

        var present = Assert.IsType<FetchOutcome.Present>(outcome);
        await using (present.Content)
        {
            Assert.Equal("final", await new StreamReader(present.Content).ReadToEndAsync(
                TestContext.Current.CancellationToken));
        }

        Assert.Equal(
            ["https://raw.communitydragon.org/relative"],
            upstream.Requests.Skip(2).Select(request => request.RequestUri!.AbsoluteUri));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task RedirectsAreCappedAtTen(int redirects, bool followed)
    {
        var upstream = FakeUpstream.Answering(request =>
        {
            var hop = int.Parse(
                request.RequestUri!.AbsolutePath.TrimStart('/'), CultureInfo.InvariantCulture);
            return hop < redirects
                ? FakeUpstream.Redirect($"{Origin}/{hop + 1}")
                : FakeUpstream.Response(HttpStatusCode.OK, "end");
        });
        using var harness = EgressHarness.Create(upstream);

        var fetch = harness.FetchAsync(Origin + "/0");

        if (followed)
        {
            Assert.IsType<FetchOutcome.Present>(await fetch);
        }
        else
        {
            await Assert.ThrowsAsync<EgressTransientException>(() => fetch);
        }
    }
}
