using System.Net;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Art;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Images.Art;
using LoDb.Ingestion.Tests.Images;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using SkiaSharp;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The WebP variants of the latest version's champion art: one fetch per art, a blob per
/// variant addressed by its bytes, each recorded once, absent when Data Dragon lacks the art;
/// and resolved for a page from what is stored alone.
/// </summary>
public sealed class ArtVariantIngestionTests(PostgresContainerFixture postgres)
{
    private const string JpegType = "image/jpeg";
    private const int Champions = 5;
    private const string CenteredFolder = "img/champion/centered/";
    private const string LoadingFolder = "img/champion/loading/";

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task LatestArtIsStoredOnceAsWebpVariants()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = await LatestWithArtAsync(harness);

        var stored = await instance.Get<IArtVariantIngestion>().IngestLatestAsync(Token);
        var again = await instance.Get<IArtVariantIngestion>().IngestLatestAsync(Token);

        Assert.Equal((Champions * 4, 0), (stored, again));
        Assert.Equal(Champions * 2, instance.Upstream.ServedRequests.Count);
        var rows = await ArtRowsAsync(harness);
        Assert.Equal(Champions * 4, rows.Count);
        Assert.All(rows, static row => Assert.Equal(
            ("webp", DdragonAssetStatus.Present),
            (row.Extension, row.Status)));
        Assert.Contains(rows, static row => row.Key == $"{CenteredFolder}FiddleSticks_0.jpg@960");
        var centered = rows.Single(static row => row.Key == $"{CenteredFolder}Ahri_0.jpg@480");
        Assert.Equal((480, 270), SizeOf(harness, centered));
        var loading = rows.Single(static row => row.Key == $"{LoadingFolder}Ahri_0.jpg@308");
        Assert.Equal((ArtVariants.LoadingWidth, 560), SizeOf(harness, loading));
    }

    [Fact]
    public async Task MissingArtIsRecordedAbsentAndResolvesToNoVariant()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = await LatestWithArtAsync(harness);
        instance.Upstream.FailNext(IsLoading, int.MaxValue, HttpStatusCode.NotFound);

        var stored = await instance.Get<IArtVariantIngestion>().IngestLatestAsync(Token);
        var ahri = Art("Ahri");
        var images = await instance.Get<IImageResolver>()
            .ResolveAsync(Latest, ahri, ColdDemand.StoredOnly, Token);

        Assert.Equal(Champions * 3, stored);
        Assert.Equal(
            [ImageStatus.Present, ImageStatus.Present, ImageStatus.Present, ImageStatus.Absent],
            ahri.Select(variant => images[variant].Status));
        Assert.All(ahri.Take(3), variant =>
            Assert.EndsWith(".webp", images[variant].Url, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoReadyVersionStoresNothing()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var stored = await instance.Get<IArtVariantIngestion>().IngestLatestAsync(Token);

        Assert.Equal(0, stored);
        Assert.Empty(instance.Upstream.ServedRequests);
    }

    // The latest version ingested and promoted, its art served from synthetic JPEGs.
    private static async Task<IngestionInstance> LatestWithArtAsync(IngestionHarness harness)
    {
        var instance = harness.StartInstance();
        await VersionIngestionTests.IngestAsync(instance, Latest);
        instance.Upstream.Serve(IsCentered, SyntheticArt.Jpeg(1280, 720), JpegType);
        instance.Upstream.Serve(
            IsLoading,
            SyntheticArt.Jpeg(ArtVariants.LoadingWidth, 560),
            JpegType);
        return instance;
    }

    private static IReadOnlyList<DdragonImage> Art(string champion) =>
    [
        .. ArtVariants.CenteredWidths.Select(width => DdragonImage.ArtVariant(
            DdragonImageKind.CenteredArt,
            $"{CenteredFolder}{champion}_0.jpg",
            width)),
        DdragonImage.ArtVariant(
            DdragonImageKind.LoadingArt,
            $"{LoadingFolder}{champion}_0.jpg",
            ArtVariants.LoadingWidth),
    ];

    private static async Task<List<DdragonAsset>> ArtRowsAsync(IngestionHarness harness) =>
    [
        .. (await harness.AssetRowsAsync(Latest.Value))
            .Where(static row => row.Type == ManifestTypes.ChampionArt),
    ];

    private static (int Width, int Height) SizeOf(IngestionHarness harness, DdragonAsset row)
    {
        var path = Path.Combine(harness.StorageRoot, "blobs", $"{row.Sha256}.{row.Extension}");
        using var decoded = SKBitmap.Decode(path);
        return (decoded.Width, decoded.Height);
    }

    private static bool IsCentered(Uri url) =>
        url.AbsolutePath.StartsWith($"/cdn/{CenteredFolder}", StringComparison.Ordinal);

    private static bool IsLoading(Uri url) =>
        url.AbsolutePath.StartsWith($"/cdn/{LoadingFolder}", StringComparison.Ordinal);
}
