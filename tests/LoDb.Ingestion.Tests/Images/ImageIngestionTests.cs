using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// Images are stored under the extension their name gives, recorded absent after a 403, and
/// fetched again when forced, without writing a stored blob twice.
/// </summary>
public sealed class ImageIngestionTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly PatchVersion FirstRunes = PatchVersion.Parse("7.22.1");
    private static readonly DdragonImage Ahri = new(DdragonImageKind.Champion, "Ahri.png");

    private static CancellationToken Token => IngestionHarness.Token;

    [Theory]
    [InlineData("Ahri.png", "png")]
    [InlineData("perk-images/Styles/7200_Domination.png", "png")]
    [InlineData("ASSETS/Perks/Styles/7200_Domination.DDS", "dds")]
    [InlineData("1001.jpeg", "jpeg")]
    [InlineData("NoExtension", "png")]
    [InlineData("Icon.extension9", "png")]
    [InlineData("Icon.p-g", "png")]
    public void ExtensionComesFromTheFileName(string file, string extension)
    {
        Assert.Equal(extension, ImageIngestion.ExtensionOf(file));
    }

    [Fact]
    public async Task ForbiddenRuneIconsAreRecordedAbsent()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var scope = new DatasetScope { Version = FirstRunes, Language = DdragonLanguage.EnUs };
        var trees = await instance.Get<IDdragonDatasets>().ReadRunesAsync(scope, Token);
        List<DdragonImage> icons = [.. trees.Entries.SelectMany(VersionImages.Of)];

        var report = await instance.Get<ImageIngestion>()
            .IngestAsync(new ImageBatch(FirstRunes, icons, Force: false), Token);

        Assert.Equal(26, icons.Count);
        Assert.Equal(icons.Count, report.Absent);
        Assert.Equal(0, report.Stored + report.Failed);
        var rows = await harness.AssetRowsAsync(FirstRunes.Value);
        Assert.Equal(icons.Count, rows.Count);
        Assert.All(rows, static row => Assert.Equal(DdragonAssetStatus.Absent, row.Status));
        Assert.Empty(harness.FilesUnder("blobs"));
    }

    [Fact]
    public async Task ForcedBatchFetchesAgainWithoutRewritingTheBlob()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var images = instance.Get<ImageIngestion>();
        var first = await images.IngestAsync(Batch(force: false), Token);
        var stored = Assert.Single(await harness.AssetRowsAsync(Latest.Value));

        var skipped = await images.IngestAsync(Batch(force: false), Token);
        var forced = await images.IngestAsync(Batch(force: true), Token);

        Assert.Equal((1, 1, 1), (first.Stored, first.BlobsWritten, first.WebpWritten));
        Assert.Equal((1, 0), (skipped.Skipped, skipped.Stored));
        Assert.Equal(
            (0, 1, 0, 0),
            (forced.Skipped, forced.Stored, forced.BlobsWritten, forced.WebpWritten));
        Assert.Equal(2, instance.Replay.Requests.Count(VersionIngestionTests.IsAhriPortrait));
        var row = Assert.Single(await harness.AssetRowsAsync(Latest.Value));
        Assert.Equal(stored.Sha256, row.Sha256);
        Assert.Equal(2, harness.FilesUnder("blobs").Count);
    }

    private static ImageBatch Batch(bool force) => new(Latest, [Ahri], force);
}
