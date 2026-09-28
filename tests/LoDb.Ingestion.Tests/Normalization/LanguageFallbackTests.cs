using System.Net;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Ingestion.Tests.Egress;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Requested language, then en_US, then an empty dataset (UP 1, UP 2); a failure is never
/// mistaken for an absence.
/// </summary>
public sealed class LanguageFallbackTests
{
    [Fact]
    public async Task ShippedLanguageIsServedAsIs()
    {
        using var harness = ReplayHarness.Create();

        var items = await harness.Datasets.ReadItemsAsync(
            ReplayHarness.Scope(DdragonFixtures.Latest.Value, "fr_FR"), ReplayHarness.Token);

        Assert.Equal("fr_FR", items.ContentLanguage);
        Assert.Equal("Bottes", Assert.Single(items.Entries, static item => item.Id == "1001").Name);
    }

    // ar_AE only exists on recent versions.
    [Fact]
    public async Task MissingLanguageFallsBackToEnglish()
    {
        using var harness = ReplayHarness.Create();

        var items = await harness.Datasets.ReadItemsAsync(
            ReplayHarness.Scope("8.7.1", "ar_AE"), ReplayHarness.Token);

        Assert.Equal("ar_AE", items.Language);
        Assert.Equal("en_US", items.ContentLanguage);
        var boots = Assert.Single(items.Entries, static item => item.Id == "1001");
        Assert.Equal("Boots of Speed", boots.Name);
    }

    // 7.22.1 ships runesReforged.json in en_US only.
    [Fact]
    public async Task MissingFileFallsBackToEnglish()
    {
        using var harness = ReplayHarness.Create();

        var runes = await harness.Datasets.ReadRunesAsync(
            ReplayHarness.Scope("7.22.1", "fr_FR"), ReplayHarness.Token);

        Assert.Equal("en_US", runes.ContentLanguage);
        Assert.Equal([8100, 8000], runes.Entries.Select(static tree => tree.Id));
    }

    // Before 7.22.1 no language ships runes: an empty dataset, persisted like any other.
    [Fact]
    public async Task FileMissingInEnglishIsAnEmptyDataset()
    {
        using var harness = ReplayHarness.Create();

        var runes = await harness.Datasets.ReadRunesAsync(
            ReplayHarness.Scope("7.21.1", "fr_FR"), ReplayHarness.Token);

        Assert.Empty(runes.Entries);
        Assert.Null(runes.ContentLanguage);
        Assert.Equal(
            ["fr_FR", "en_US"],
            harness.Replay.Requests.Select(static url => url.Segments[^2].TrimEnd('/')));
    }

    // A 503 says nothing about the file: falling back to en_US would persist English under
    // fr_FR for good.
    [Fact]
    public async Task OutageNeitherFallsBackNorReturnsADocument()
    {
        using var harness = ReplayHarness.Create();
        var scope = ReplayHarness.Scope(DdragonFixtures.Latest.Value, "fr_FR");
        var french = DdragonUrls.Dataset(scope.Version, scope.Language, "item");
        harness.Replay.FailWith(url => url == french, HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.Datasets.ReadItemsAsync(scope, ReplayHarness.Token));

        Assert.All(harness.Replay.Requests, url => Assert.Equal(french, url));
    }

    [Fact]
    public async Task UnreadableFileNeitherFallsBackNorReturnsADocument()
    {
        using var harness = ReplayHarness.Over(
            FakeUpstream.Answering(HttpStatusCode.OK, "{\"data\": [\"not a map\"]}"));

        await Assert.ThrowsAsync<UpstreamDocumentException>(
            () => harness.Datasets.ReadSummonersAsync(
                ReplayHarness.Scope("8.7.1", "fr_FR"), ReplayHarness.Token));
    }
}
