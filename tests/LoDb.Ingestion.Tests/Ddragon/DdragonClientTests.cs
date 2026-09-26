using System.Net;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Tests.Egress;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Ddragon;

public sealed class DdragonClientTests
{
    // versions.json still lists lolpatch_7.20 and lolpatch_7.19 after 0.151.2 (UP 4).
    [Fact]
    public async Task VersionsDropLegacyEntriesAndStartWithTheLatest()
    {
        using var harness = ReplayHarness.Create();

        var versions = await harness.Client.GetVersionsAsync(ReplayHarness.Token);

        Assert.Equal(DdragonFixtures.Versions, versions);
        Assert.Equal(DdragonFixtures.Latest, versions[0]);
        Assert.Equal(PatchVersion.Parse("0.151.2"), versions[^1]);
        Assert.DoesNotContain(
            versions,
            static version => version.Value.StartsWith("lolpatch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LanguagesKeepTheUpstreamOrder()
    {
        using var harness = ReplayHarness.Create();

        var languages = await harness.Client.GetLanguagesAsync(ReplayHarness.Token);

        Assert.Equal(
            ["ar_AE", "en_US", "fr_FR", "ko_KR", "zh_CN"],
            languages.Select(static language => language.Code));
        Assert.Contains(DdragonLanguage.EnUs, languages);
    }

    // An absent list would read as "no version at all": the ingestion must stop instead.
    [Fact]
    public async Task AbsentVersionListIsAnUpstreamFault()
    {
        using var harness = ReplayHarness.Create();
        harness.Replay.FailWith(static url => url == DdragonUrls.Versions, HttpStatusCode.NotFound);

        var fault = await Assert.ThrowsAsync<UpstreamDocumentException>(
            () => harness.Client.GetVersionsAsync(ReplayHarness.Token));

        Assert.Equal(DdragonUrls.Versions, fault.Url);
    }

    [Fact]
    public async Task OutageOfTheListIsTransient()
    {
        using var harness = ReplayHarness.Create();
        harness.Replay.FailWith(
            static url => url == DdragonUrls.Languages, HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.Client.GetLanguagesAsync(ReplayHarness.Token));
    }

    [Fact]
    public async Task UnreadableListIsAnUpstreamFault()
    {
        using var harness = ReplayHarness.Over(
            FakeUpstream.Answering(HttpStatusCode.OK, "{\"not\": \"a list\"}"));

        var fault = await Assert.ThrowsAsync<UpstreamDocumentException>(
            () => harness.Client.GetVersionsAsync(ReplayHarness.Token));

        Assert.IsType<System.Text.Json.JsonException>(fault.InnerException);
    }
}
