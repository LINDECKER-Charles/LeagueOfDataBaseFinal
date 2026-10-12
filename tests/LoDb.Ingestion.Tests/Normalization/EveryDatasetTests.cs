using System.Net;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Normalization.Serialization;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Every dataset type of every recorded version reads into a complete document that survives
/// storage, in a language the version ships and in one it falls back from.
/// </summary>
public sealed class EveryDatasetTests
{
    // Every recorded version ships fr_FR. Other shipped languages (ko_KR, zh_CN) take the same
    // path: only the version's shapes vary, so one shipped language per version suffices.
    private const string ShippedLanguage = "fr_FR";

    // Recorded as absent (403) except on the two full versions: there it falls back to en_US.
    private const string FallbackLanguage = "ar_AE";

    private const string ProbedDataset = "item";

    private static readonly PatchVersion FirstRunesVersion = PatchVersion.Parse("7.22.1");

    public static TheoryData<string, string> Scopes
    {
        get
        {
            var scopes = new TheoryData<string, string>();
            foreach (var version in DdragonFixtures.Versions)
            {
                scopes.Add(version.Value, ShippedLanguage);
                if (!IsShipped(version, FallbackLanguage))
                {
                    scopes.Add(version.Value, FallbackLanguage);
                }
            }

            return scopes;
        }
    }

    [Theory]
    [MemberData(nameof(Scopes))]
    public async Task EveryTypeReadsAndRoundTrips(string version, string language)
    {
        using var harness = ReplayHarness.Create();
        var scope = ReplayHarness.Scope(version, language);
        var chromas = await harness.Datasets.ReadChromasAsync(scope.Version, ReplayHarness.Token);

        var champions = await harness.Datasets.ReadChampionsAsync(
            scope, chromas, ReplayHarness.Token);
        var items = await harness.Datasets.ReadItemsAsync(scope, ReplayHarness.Token);
        var summoners = await harness.Datasets.ReadSummonersAsync(scope, ReplayHarness.Token);
        var runes = await harness.Datasets.ReadRunesAsync(scope, ReplayHarness.Token);

        AssertComplete(scope, champions);
        AssertComplete(scope, items);
        AssertComplete(scope, summoners);
        Assert.Equal(scope.Version >= FirstRunesVersion, runes.Entries.Count > 0);
        Assert.Equal(runes.Entries.Count > 0, runes.ContentLanguage is not null);
        await AssertRoundTripsAsync(DatasetTypes.Champions, champions);
        await AssertRoundTripsAsync(DatasetTypes.Items, items);
        await AssertRoundTripsAsync(DatasetTypes.Summoners, summoners);
        await AssertRoundTripsAsync(DatasetTypes.Runes, runes);
    }

    private static bool IsShipped(PatchVersion version, string language)
    {
        var url = DdragonUrls.Dataset(version, DdragonLanguage.Parse(language), ProbedDataset);
        return DdragonFixtures.Index.Responses.Any(response =>
            response.Url == url.AbsoluteUri && response.Status == (int)HttpStatusCode.OK);
    }

    // Every recorded version ships these three in en_US at least, whatever the language.
    private static void AssertComplete<TEntry>(DatasetScope scope, DatasetDocument<TEntry> document)
    {
        Assert.Equal(scope.Version.Value, document.Version);
        Assert.Equal(scope.Language.Code, document.Language);
        Assert.Contains(
            document.ContentLanguage,
            new[] { scope.Language.Code, DdragonLanguage.EnUs.Code });
        Assert.NotEmpty(document.Entries);
    }

    private static async Task AssertRoundTripsAsync<TEntry>(
        DatasetType<TEntry> type,
        DatasetDocument<TEntry> document)
    {
        var stored = DatasetSerializer.Serialize(type, document);
        using var json = new MemoryStream(stored);

        var read = await DatasetSerializer.DeserializeAsync(type, json, ReplayHarness.Token);

        Assert.Equal(stored, DatasetSerializer.Serialize(type, read));
    }
}
