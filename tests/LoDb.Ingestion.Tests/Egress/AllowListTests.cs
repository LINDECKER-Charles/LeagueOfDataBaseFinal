using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class AllowListTests
{
    [Theory]
    [InlineData(EgressHarness.DdragonUrl)]
    [InlineData("https://raw.communitydragon.org/latest/plugins/x.json")]
    [InlineData("https://DDRAGON.leagueoflegends.com:443/cdn/languages.json")]
    public async Task AllowedHostIsFetched(string url)
    {
        var upstream = FakeUpstream.Answering(HttpStatusCode.OK, "[]");
        using var harness = EgressHarness.Create(upstream);

        var outcome = await harness.FetchAsync(url);

        Assert.IsType<FetchOutcome.Present>(outcome);
        Assert.Equal(1, upstream.Calls);
    }

    [Theory]
    [InlineData("http://ddragon.leagueoflegends.com/x", EgressRefusal.Scheme)]
    [InlineData("ftp://ddragon.leagueoflegends.com/x", EgressRefusal.Scheme)]
    [InlineData("https://evil.example.com/x", EgressRefusal.Host)]
    [InlineData("https://ddragon.leagueoflegends.com.evil.example/x", EgressRefusal.Host)]
    [InlineData("https://ddragon.leagueoflegends.com@evil.example/x", EgressRefusal.Host)]
    [InlineData("https://169.254.169.254/latest/meta-data", EgressRefusal.Host)]
    [InlineData("/api/versions.json", EgressRefusal.NotAbsolute)]
    public async Task RefusedUrlNeverLeavesTheProcess(string url, EgressRefusal reason)
    {
        var upstream = FakeUpstream.Answering(HttpStatusCode.OK);
        using var harness = EgressHarness.Create(upstream);

        var refusal = await Assert.ThrowsAsync<EgressRefusedException>(
            () => harness.FetchAsync(url));

        Assert.Equal(reason, refusal.Reason);
        Assert.Equal(0, upstream.Calls);
    }

    [Fact]
    public async Task ConfiguredHostsReplaceTheDefaults()
    {
        var upstream = FakeUpstream.Answering(HttpStatusCode.OK);
        using var harness = EgressHarness.Create(
            upstream,
            new Dictionary<string, string?> { ["LoDb:Egress:AllowedHosts:0"] = "mirror.test" });

        await harness.FetchAsync("https://mirror.test/api/versions.json");
        await Assert.ThrowsAsync<EgressRefusedException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.Equal(1, upstream.Calls);
    }

    // The guard lives in the client, not only in the fetcher: a direct use is filtered too.
    [Fact]
    public async Task NamedClientUsedDirectlyIsFiltered()
    {
        var upstream = FakeUpstream.Answering(HttpStatusCode.OK);
        using var harness = EgressHarness.Create(upstream);
        var client = harness.Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(EgressRegistration.ClientName);

        await Assert.ThrowsAsync<EgressRefusedException>(
            () => client.GetAsync(
                new Uri("https://evil.example.com/"), TestContext.Current.CancellationToken));

        Assert.Equal(0, upstream.Calls);
    }
}
