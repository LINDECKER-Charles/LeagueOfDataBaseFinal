using System.Net;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// A version is ingested whole (datasets of every language, then the images they name), once,
/// and promoted only when it is complete and the newest.
/// </summary>
public sealed class VersionIngestionTests(PostgresContainerFixture postgres)
{
    /// <summary>
    /// The images of the recorded latest version: 5 portraits, 5 passives, 20 champion spells,
    /// 18 items, 6 summoner spells and 27 rune icons.
    /// </summary>
    private const int LatestImages = 81;

    /// <summary>The two lists every run reads: nothing else when all is stored.</summary>
    internal static readonly IReadOnlyList<string> ListDocuments =
        ["/api/versions.json", "/cdn/languages.json"];

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly PatchVersion Previous = DdragonFixtures.Previous;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task CompleteRunStoresEverythingThenPromotes()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        using var ready = Collect(instance, "lodb.ingestion.versions.ready");

        var result = await IngestAsync(instance, Latest);

        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        Assert.Equal(20, result.DatasetsWritten);
        Assert.True(result.Promoted);
        Assert.Equal(20, harness.FilesUnder($"data/{Latest.Value}").Count);
        var rows = await harness.AssetRowsAsync(Latest.Value);
        Assert.Equal(await ExpectedKeysAsync(instance, Latest), Keys(rows));
        Assert.Equal(LatestImages, rows.Count);
        Assert.All(rows, row => AssertStoredWithWebp(harness, row));
        var version = await harness.VersionRowAsync(Latest.Value);
        Assert.NotNull(version);
        Assert.Equal(DdragonVersionStatus.Ready, version.Status);
        Assert.Equal(1, version.Attempts);
        Assert.Null(version.NextAttemptAt);
        Assert.Equal(IngestionHarness.Start, version.ReadyAt);
        Assert.Equal(IngestionHarness.Start, version.PromotedAt);
        Assert.Equal(1, Assert.Single(ready.GetMeasurementSnapshot()).Value);
        Assert.Contains(instance.Logs, static log => log.Id.Name == "ingest.version.completed");
    }

    [Fact]
    public async Task RerunFetchesNothingButTheLists()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        await IngestAsync(harness.StartInstance(), Latest);
        var rerun = harness.StartInstance();

        var result = await IngestAsync(rerun, Latest);

        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.DatasetsWritten);
        Assert.NotNull(result.Images);
        Assert.Equal(LatestImages, result.Images.Skipped);
        Assert.Equal(0, result.Images.Stored);
        Assert.False(result.Promoted);
        Assert.Equal(ListDocuments, rerun.RequestedPaths().Order(StringComparer.Ordinal));
        var version = await harness.VersionRowAsync(Latest.Value);
        Assert.Equal(1, version?.Attempts);
    }

    [Fact]
    public async Task PartialRunLeavesTheVersionStateAlone()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var request = new IngestionRequest { Languages = [DdragonLanguage.EnUs] };

        var result = await harness.StartInstance().Get<IVersionIngestion>()
            .IngestAsync(Latest, request, Token);

        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        Assert.Equal(4, result.DatasetsWritten);
        Assert.False(result.Promoted);
        Assert.Equal(4, harness.FilesUnder($"data/{Latest.Value}/en_US").Count);
        Assert.Equal(LatestImages, (await harness.AssetRowsAsync(Latest.Value)).Count);
        Assert.Null(await harness.VersionRowAsync(Latest.Value));
    }

    [Fact]
    public async Task OlderVersionCompletedLaterIsNotPromoted()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        await IngestAsync(instance, Latest);

        var older = await IngestAsync(instance, Previous);

        Assert.Equal(VersionIngestionOutcome.Completed, older.Outcome);
        Assert.False(older.Promoted);
        var row = await harness.VersionRowAsync(Previous.Value);
        Assert.Equal(DdragonVersionStatus.Ready, row?.Status);
        Assert.Null(row?.PromotedAt);
        Assert.Equal(Latest, await instance.Get<VersionStates>().LatestPromotedAsync(Token));
    }

    [Fact]
    public async Task NewerVersionTakesOverAsTheLatest()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var older = await IngestAsync(instance, Previous);
        var newer = await IngestAsync(instance, Latest);

        Assert.True(older.Promoted);
        Assert.True(newer.Promoted);
        Assert.Equal(Latest, await instance.Get<VersionStates>().LatestPromotedAsync(Token));
    }

    [Theory]
    [InlineData("99.1.1", "en_US")]
    [InlineData("16.19.1", "de_DE")]
    public async Task UnlistedVersionOrLanguageIsRefused(string version, string language)
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var request = new IngestionRequest { Languages = [DdragonLanguage.Parse(language)] };

        var result = await instance.Get<IVersionIngestion>()
            .IngestAsync(PatchVersion.Parse(version), request, Token);

        Assert.Equal(VersionIngestionOutcome.Unknown, result.Outcome);
        Assert.All(instance.RequestedPaths(), static path => Assert.Contains(path, ListDocuments));
        Assert.Empty(harness.FilesUnder("data"));
        Assert.Null(await harness.VersionRowAsync(version));
        Assert.Contains(instance.Logs, static log => log.Id.Name == "ingest.version.unknown");
    }

    [Fact]
    public async Task ForbiddenImageIsRecordedAbsentAndNeverFetchedAgain()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var first = harness.StartInstance();
        first.Replay.FailWith(static url => IsAhriPortrait(url), HttpStatusCode.Forbidden);
        using var absences = Collect(first, "lodb.ingestion.absences");

        var result = await IngestAsync(first, Latest);
        var rerun = harness.StartInstance();
        await IngestAsync(rerun, Latest);

        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        Assert.True(result.Promoted);
        var ahri = Assert.Single(
            await harness.AssetRowsAsync(Latest.Value),
            static row => row is { Type: ManifestTypes.Champion, Key: "Ahri.png" });
        Assert.Equal(DdragonAssetStatus.Absent, ahri.Status);
        Assert.Null(ahri.Sha256);
        var absence = Assert.Single(absences.GetMeasurementSnapshot());
        Assert.Equal(ManifestTypes.Champion, absence.Tags[IngestionMetrics.TypeTag]);
        Assert.DoesNotContain(rerun.Replay.Requests, static url => IsAhriPortrait(url));
    }

    internal static Task<VersionIngestionResult> IngestAsync(
        IngestionInstance instance,
        PatchVersion version,
        IngestionRequest? request = null) =>
        instance.Get<IVersionIngestion>()
            .IngestAsync(version, request ?? IngestionRequest.Complete, Token);

    /// <summary>The portrait of Ahri, the first champion of the recording.</summary>
    internal static bool IsAhriPortrait(Uri url) =>
        url.AbsolutePath.EndsWith("/img/champion/Ahri.png", StringComparison.Ordinal);

    internal static MetricCollector<long> Collect(IngestionInstance instance, string instrument) =>
        new(instance.Meters, IngestionMetrics.MeterName, instrument);

    internal static IReadOnlyList<string> Keys(IEnumerable<DdragonAsset> rows) =>
        [.. rows.Select(static row => $"{row.Type}/{row.Key}").Order(StringComparer.Ordinal)];

    private static async Task<IReadOnlyList<string>> ExpectedKeysAsync(
        IngestionInstance instance,
        PatchVersion version)
    {
        var scope = new DatasetScope { Version = version, Language = DdragonLanguage.EnUs };
        var datasets = await instance.Get<StoredDatasets>().LoadAsync(scope, Token);
        Assert.NotNull(datasets);
        return
        [
            .. VersionImages.Of(datasets)
                .Select(static image => $"{image.ManifestType}/{image.File}")
                .Order(StringComparer.Ordinal),
        ];
    }

    private static void AssertStoredWithWebp(IngestionHarness harness, DdragonAsset row)
    {
        Assert.Equal(DdragonAssetStatus.Present, row.Status);
        var key = new BlobKey(row.Sha256!, row.Extension!);
        Assert.True(File.Exists(Path.Combine(harness.StorageRoot, key.RelativePath)), row.Key);
        var webp = File.ReadAllBytes(Path.Combine(harness.StorageRoot, key.WebpRelativePath!));
        Assert.Equal("RIFF"u8.ToArray(), webp[..4]);
        Assert.Equal("WEBP"u8.ToArray(), webp[8..12]);
    }
}
