using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Http;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class TransportTests
{
    // The image CDN offers no h2: every request, redirect hops included, asks for 1.1 only.
    [Fact]
    public async Task RequestsUseHttp11Exactly()
    {
        var upstream = FakeUpstream.Answering(request => request.RequestUri!.AbsolutePath == "/a"
            ? FakeUpstream.Redirect("/b")
            : FakeUpstream.Response(HttpStatusCode.OK, "ok"));
        using var harness = EgressHarness.Create(upstream);

        await using var content = Assert.IsType<FetchOutcome.Present>(
            await harness.FetchAsync("https://ddragon.leagueoflegends.com/a")).Content;

        Assert.Equal(2, upstream.Calls);
        Assert.All(upstream.Requests, request =>
        {
            Assert.Equal(HttpVersion.Version11, request.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy);
        });
    }

    [Theory]
    [InlineData(16)]
    [InlineData(4)]
    public void PrimaryHandlerNeverRedirectsAndPoolsOneConnectionPerFetch(int concurrency)
    {
        using var handler = EgressPrimaryHandler.Create(
            new EgressOptions { FetchConcurrency = concurrency });

        Assert.False(handler.AllowAutoRedirect);
        Assert.Equal(concurrency, handler.MaxConnectionsPerServer);
    }

    [Fact]
    public void DefaultConcurrencyIsSixteen()
    {
        Assert.Equal(16, new EgressOptions().FetchConcurrency);
    }
}
