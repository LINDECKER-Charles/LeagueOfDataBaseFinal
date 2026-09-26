using System.Text.Json;
using LoDb.Api.Hosting.Health;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/storage</c>: the objects of the storage root by family, the images and
/// their WebP siblings, the datasets and what the content addressing saves.
/// </summary>
public sealed class AdminStorageTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string StoragePath = "/api/admin/storage?refresh=true";

    private static readonly string Portrait = new('a', 64);
    private static readonly string Splash = new('b', 64);

    [Fact]
    public async Task TheReportSortsTheObjectsOfTheRoot()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await LayOutAsync();

        var report = await ReadAsync(admin, StoragePath);

        Assert.True(report.GetProperty("ok").GetBoolean());
        Assert.Equal((7, 99), (Int(report, "objects"), Int(report, "bytes")));
        Assert.Equal(
            ["data:60", "blobs:34", "other:5"],
            Rows(report.GetProperty("families")));
        var blobs = report.GetProperty("blobs");
        Assert.Equal(["jpg:20", "png:10", "webp:4"], Rows(blobs.GetProperty("byExt")));
        Assert.Equal(
            (1, 1, 1.0, 10, 4),
            (Int(blobs, "sources"), Int(blobs, "webpSiblings"),
                blobs.GetProperty("webpCoverage").GetDouble(), Int(blobs, "sourceBytes"),
                Int(blobs, "webpBytes")));
        var data = report.GetProperty("data");
        Assert.Equal(["16.19.1:40", "16.9.1:20"], Rows(data.GetProperty("byVersion")));
        Assert.Equal(["champion:40", "item:20"], Rows(data.GetProperty("byType")));
        var coverage = report.GetProperty("coverage").EnumerateArray().ToList();
        Assert.Equal(
            ["16.19.1", "16.9.1"],
            coverage.Select(static version => ApiJson.Text(version, "version")));
        Assert.Equal(
            ["en_US", "fr_FR"],
            coverage[0].GetProperty("langs").EnumerateArray().Select(static l => l.GetString()));
        Assert.Equal("data/16.19.1/fr_FR/champion.json", ApiJson.Text(
            report.GetProperty("largest")[0], "path"));
    }

    [Fact]
    public async Task TheSavingsCountTheReferencesBeyondTheBlobs()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await LayOutAsync();
        await AdminSeed.WithContextAsync(App, async context =>
        {
            context.DdragonAssets.AddRange(
                Asset("16.19.1", "Ahri", Portrait),
                Asset("16.9.1", "Ahri", Portrait),
                Asset("16.19.1", "Garen", Splash),
                Asset("16.19.1", "Lux", sha: null));
            return await context.SaveChangesAsync(Cancellation);
        });

        var dedup = (await ReadAsync(admin, StoragePath)).GetProperty("dedup");

        // Two blobs besides the sibling, of 15 bytes on average; one reference beyond them.
        Assert.Equal(
            (3, 2, 1.5, 15),
            (Int(dedup, "logicalRefs"), Int(dedup, "physicalBlobs"),
                dedup.GetProperty("ratio").GetDouble(), Int(dedup, "savedBytesApprox")));
    }

    [Fact]
    public async Task TheReportIsKeptUntilARefreshIsAsked()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var first = await ReadAsync(admin, StoragePath);
        await WriteAsync("other/late.txt", 7);

        var cached = await ReadAsync(admin, "/api/admin/storage");
        var refreshed = await ReadAsync(admin, StoragePath);

        Assert.Equal(
            (0, 0, 1),
            (Int(first, "objects"), Int(cached, "objects"), Int(refreshed, "objects")));
    }

    private static int Int(JsonElement element, string name) =>
        element.GetProperty(name).GetInt32();

    private static List<string> Rows(JsonElement rows) =>
        [
            .. rows.EnumerateArray().Select(static row =>
                $"{row.GetProperty("name").GetString()}:{row.GetProperty("bytes").GetInt64()}"),
        ];

    private static DdragonAsset Asset(string version, string key, string? sha) => new()
    {
        Version = version,
        Type = "champion",
        Key = key,
        Status = sha is null ? DdragonAssetStatus.Absent : DdragonAssetStatus.Present,
        Sha256 = sha,
        Extension = sha is null ? null : "png",
        RecordedAt = DateTimeOffset.UnixEpoch,
    };

    // Seven objects, 99 bytes: what the staging area and the probes hold is not counted.
    private async Task LayOutAsync()
    {
        await WriteAsync($"blobs/{Portrait}.png", 10);
        await WriteAsync($"blobs/{Portrait}.webp", 4);
        await WriteAsync($"blobs/{Splash}.jpg", 20);
        await WriteAsync("data/16.19.1/fr_FR/champion.json", 21);
        await WriteAsync("data/16.19.1/en_US/champion.json", 19);
        await WriteAsync("data/16.9.1/fr_FR/item.json", 20);
        await WriteAsync("other/notes.txt", 5);
        await WriteAsync(".staging/partial", 100);
        await WriteAsync(".readyz-probe", 1);
    }

    private async Task WriteAsync(string path, int bytes)
    {
        var configuration = App.Services.GetRequiredService<IConfiguration>();
        var root = configuration[StorageReadinessCheck.RootKey]!;
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, new byte[bytes], Cancellation);
    }
}
