using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class RefusalLogTests
{
    [Fact]
    public async Task AllowListRefusalsOfABatchMakeOneLine()
    {
        using var harness = EgressHarness.Create(FakeUpstream.Answering(HttpStatusCode.OK));

        using (var batch = harness.Fetcher.OpenBatch())
        {
            await FetchAllAsync(
                batch,
                "https://a.example/1",
                "https://a.example/2",
                "http://ddragon.leagueoflegends.com/3",
                EgressHarness.DdragonUrl);
        }

        var line = Assert.Single(harness.Lines);
        Assert.Equal("fetch.allowlist.refused", line.Id.Name);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Equal("3", line.GetStructuredStateValue("Refused"));
        Assert.Equal("4", line.GetStructuredStateValue("Batch"));
        Assert.Equal(
            "a.example,ddragon.leagueoflegends.com", line.GetStructuredStateValue("Hosts"));
    }

    [Fact]
    public async Task RedirectRefusalsOfABatchMakeOneLineOfTheirOwn()
    {
        using var harness = EgressHarness.Create(
            FakeUpstream.Answering(_ => FakeUpstream.Redirect("https://evil.example/")));

        using (var batch = harness.Fetcher.OpenBatch())
        {
            await FetchAllAsync(batch, EgressHarness.DdragonUrl, EgressHarness.DdragonUrl);
        }

        var line = Assert.Single(harness.Lines);
        Assert.Equal("fetch.redirect.refused", line.Id.Name);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Equal("2", line.GetStructuredStateValue("Refused"));
    }

    [Fact]
    public async Task SingleFetchIsABatchOfOne()
    {
        using var harness = EgressHarness.Create(FakeUpstream.Answering(HttpStatusCode.OK));

        await Assert.ThrowsAsync<EgressRefusedException>(
            () => harness.FetchAsync("https://evil.example/"));

        var line = Assert.Single(harness.Lines);
        Assert.Equal("fetch.allowlist.refused", line.Id.Name);
        Assert.Equal("1", line.GetStructuredStateValue("Batch"));
    }

    [Fact]
    public async Task TransientFailuresMakeOneDegradedWarning()
    {
        using var harness = EgressHarness.Create(
            FakeUpstream.Answering(HttpStatusCode.Gone));

        using (var batch = harness.Fetcher.OpenBatch())
        {
            await FetchAllAsync(batch, EgressHarness.DdragonUrl, EgressHarness.DdragonUrl);
        }

        var line = Assert.Single(harness.Lines);
        Assert.Equal("fetch.batch.degraded", line.Id.Name);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal("2", line.GetStructuredStateValue("Failed"));
    }

    [Fact]
    public async Task VerdictsAloneLogNothing()
    {
        using var harness = EgressHarness.Create(FakeUpstream.Answering(HttpStatusCode.NotFound));

        using (var batch = harness.Fetcher.OpenBatch())
        {
            await FetchAllAsync(batch, EgressHarness.DdragonUrl, EgressHarness.DdragonUrl);
        }

        Assert.Empty(harness.Lines);
    }

    private static async Task FetchAllAsync(IEgressBatch batch, params string[] urls)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var url in urls)
        {
            try
            {
                if (await batch.FetchAsync(new Uri(url), token) is FetchOutcome.Present present)
                {
                    await present.Content.DisposeAsync();
                }
            }
            catch (EgressException)
            {
                // Counted by the batch, reported on disposal.
            }
        }
    }
}
