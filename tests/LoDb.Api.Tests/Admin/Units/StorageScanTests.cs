using LoDb.Api.Modules.Admin.Storage;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>
/// One walk of a storage root: each object sorted by the layout of ADR 0004, blobs by
/// extension with the PNGs and WebPs by digest, datasets by version, language and type, and
/// the staging area and hidden probes left out.
/// </summary>
public sealed class StorageScanTests : IDisposable
{
    private static readonly string Portrait = new('a', 64);
    private static readonly string Splash = new('b', 64);

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("lodb-storage-scan-");

    public StorageScanTests()
    {
        Write($"blobs/{Portrait}.png", 10);
        Write($"blobs/{Portrait}.webp", 4);
        Write($"blobs/{Splash}.jpg", 20);
        Write("data/16.19.1/fr_FR/champion.json", 21);
        Write("data/16.19.1/en_US/champion.json", 19);
        Write("data/16.9.1/fr_FR/item.json", 20);
        Write("other/notes.txt", 5);
        Write(".staging/partial", 100);
        Write(".readyz-probe", 1);
    }

    [Fact]
    public void ObjectsAreSortedByFamilyWithoutTheStagingAreaNorTheProbes()
    {
        var scan = StorageScan.Of(_root, TestContext.Current.CancellationToken);

        Assert.Equal(["data:60", "blobs:34", "other:5"], Rows(scan.Families));
        Assert.Equal(["jpg:20", "png:10", "webp:4"], Rows(scan.Extensions));
        Assert.Equal(
            (Portrait, 10L, Portrait, 4L),
            (Assert.Single(scan.Pngs).Key, scan.Pngs[Portrait],
                Assert.Single(scan.Webps).Key, scan.Webps[Portrait]));
        Assert.Equal("data/16.19.1/fr_FR/champion.json", scan.Largest()[0].Path);
    }

    [Fact]
    public void DatasetsAreCountedByVersionLanguageAndType()
    {
        var scan = StorageScan.Of(_root, TestContext.Current.CancellationToken);

        Assert.Equal(["16.19.1:40", "16.9.1:20"], Rows(scan.Versions));
        Assert.Equal(["champion:40", "item:20"], Rows(scan.Types));
        var coverage = scan.Coverage.Rows();
        Assert.Equal(["16.19.1", "16.9.1"], coverage.Select(static version => version.Version));
        Assert.Equal(["en_US", "fr_FR"], coverage[0].Langs);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static List<string> Rows(StorageTally tally) =>
        [.. tally.Rows().Select(static row => $"{row.Name}:{row.Bytes}")];

    private void Write(string path, int bytes)
    {
        var full = Path.Combine(_root.FullName, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[bytes]);
    }
}
