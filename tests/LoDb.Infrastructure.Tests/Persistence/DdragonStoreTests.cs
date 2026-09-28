using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The lot 1 tables, written by N instances at once: upserts on one key never fail nor
/// duplicate, an absence is replaced only by a forced ingestion, the checks refuse bad rows.
/// </summary>
public sealed class DdragonStoreTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Version = "16.19.1";
    private const string Type = "champion";
    private const int Writers = 8;

    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero));

    private ServiceProvider? _services;

    public static TheoryData<DdragonAssetStatus, string?, string?, string> InvalidAssets => new()
    {
        { DdragonAssetStatus.Present, null, "png", "ck_ddragon_asset_blob" },
        { DdragonAssetStatus.Present, Sha(1), null, "ck_ddragon_asset_blob" },
        { DdragonAssetStatus.Absent, Sha(1), "png", "ck_ddragon_asset_blob" },
        { DdragonAssetStatus.Present, Sha(1).ToUpperInvariant(), "png", "ck_ddragon_asset_sha256" },
        { DdragonAssetStatus.Present, Sha(1)[..63], "png", "ck_ddragon_asset_sha256" },
        { DdragonAssetStatus.Present, Sha(1), ".png", "ck_ddragon_asset_extension" },
    };

    private IDdragonAssetStore Assets => Services.GetRequiredService<IDdragonAssetStore>();

    private IDdragonVersionStore Versions => Services.GetRequiredService<IDdragonVersionStore>();

    private ServiceProvider Services =>
        _services ??= PersistenceServices.Build(Database.ConnectionString, _time);

    [Fact]
    public async Task ConcurrentRecordsOfOneKeyInsertOneRow()
    {
        var written = await Task.WhenAll(Enumerable.Range(0, Writers).Select(writer =>
            Assets.RecordAsync([Present("Ahri", writer)], force: false, Cancellation)));

        Assert.Equal(1, written.Sum());
        var asset = Assert.Single(await Assets.ListAsync(Version, Type, Cancellation));
        Assert.Contains(asset.Sha256!, Enumerable.Range(0, Writers).Select(Sha));
    }

    [Fact]
    public async Task ConcurrentForcedBatchesInAnyOrderNeverDeadlock()
    {
        var keys = Enumerable.Range(0, 200).Select(static key => $"Champion{key:D3}").ToList();

        await Task.WhenAll(Enumerable.Range(0, Writers).Select(writer =>
            Assets.RecordAsync(
                [.. InWriterOrder(keys, writer).Select(key => Present(key, writer))],
                force: true,
                Cancellation)));

        var assets = await Assets.ListAsync(Version, Type, Cancellation);
        Assert.Equal(keys, assets.Select(static asset => asset.Key));
    }

    [Fact]
    public async Task AbsenceIsReplacedOnlyWhenForced()
    {
        await Assets.RecordAsync([Absent("Hwei")], force: false, Cancellation);
        var recordedAt = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromHours(1));

        var kept = await Assets.RecordAsync([Present("Hwei", 1)], force: false, Cancellation);
        var absent = Assert.Single(await Assets.ListAsync(Version, Type, Cancellation));
        var replaced = await Assets.RecordAsync([Present("Hwei", 1)], force: true, Cancellation);
        var present = Assert.Single(await Assets.ListAsync(Version, Type, Cancellation));

        Assert.Equal(0, kept);
        Assert.Equal(DdragonAssetStatus.Absent, absent.Status);
        Assert.Equal(recordedAt, absent.RecordedAt);
        Assert.Equal(1, replaced);
        Assert.Equal(DdragonAssetStatus.Present, present.Status);
        Assert.Equal(Sha(1), present.Sha256);
        Assert.Equal("png", present.Extension);
        Assert.Equal(_time.GetUtcNow(), present.RecordedAt);
    }

    [Fact]
    public async Task UnchangedForcedRecordWritesNothing()
    {
        await Assets.RecordAsync([Present("Ahri", 1)], force: false, Cancellation);
        var recordedAt = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromHours(1));

        var written = await Assets.RecordAsync([Present("Ahri", 1)], force: true, Cancellation);

        Assert.Equal(0, written);
        var asset = Assert.Single(await Assets.ListAsync(Version, Type, Cancellation));
        Assert.Equal(recordedAt, asset.RecordedAt);
    }

    [Fact]
    public async Task LastEntryOfAKeyWinsWithinABatch()
    {
        var written = await Assets.RecordAsync(
            [Absent("Ahri"), Present("Ahri", 2), Present("Zed", 3)],
            force: false,
            Cancellation);

        Assert.Equal(2, written);
        var assets = await Assets.ListAsync(Version, Type, Cancellation);
        Assert.Equal(["Ahri", "Zed"], assets.Select(static asset => asset.Key));
        Assert.Equal(Sha(2), assets[0].Sha256);
    }

    [Fact]
    public async Task EmptyBatchWritesNothing() =>
        Assert.Equal(0, await Assets.RecordAsync([], force: true, Cancellation));

    [Fact]
    public async Task ListReturnsOneVersionAndTypeByKey()
    {
        await Assets.RecordAsync(
            [
                Present("Zed", 1),
                Present("Ahri", 2),
                Absent("Hwei"),
                DdragonAssetEntry.Absent("16.18.1", Type, "Annie"),
                DdragonAssetEntry.Absent(Version, "item", "1001"),
            ],
            force: false,
            Cancellation);

        var assets = await Assets.ListAsync(Version, Type, Cancellation);

        Assert.Equal(["Ahri", "Hwei", "Zed"], assets.Select(static asset => asset.Key));
    }

    [Theory]
    [MemberData(nameof(InvalidAssets))]
    public async Task ChecksRefuseInconsistentAssets(
        DdragonAssetStatus status,
        string? sha256,
        string? extension,
        string constraint)
    {
        var entry = new DdragonAssetEntry(Version, Type, "Ahri", status, sha256, extension);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            Assets.RecordAsync([entry], force: false, Cancellation));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Theory]
    [InlineData("status", "'bogus'", "ck_ddragon_version_status")]
    [InlineData("attempts", "-1", "ck_ddragon_version_attempts")]
    public async Task ChecksRefuseInconsistentVersions(
        string column,
        string value,
        string constraint)
    {
        await Versions.DiscoverAsync(Version, Cancellation);

        var error = await Assert.ThrowsAsync<PostgresException>(() => Database.ExecuteAsync(
            $"UPDATE ddragon_version SET {column} = {value}",
            Cancellation));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task ConcurrentDiscoveriesOfAVersionInsertItOnce()
    {
        var inserted = await Task.WhenAll(Enumerable.Range(0, Writers).Select(_ =>
            Versions.DiscoverAsync(Version, Cancellation)));

        Assert.Single(inserted, static wasInserted => wasInserted);
        var version = Assert.Single(await Versions.ListAsync(Cancellation));
        Assert.Equal(Version, version.Version);
        Assert.Equal(DdragonVersionStatus.Discovered, version.Status);
        Assert.Equal(0, version.Attempts);
        Assert.Equal(_time.GetUtcNow(), version.DiscoveredAt);
        Assert.Equal(_time.GetUtcNow(), version.UpdatedAt);
        Assert.Null(version.NextAttemptAt);
        Assert.Null(version.ReadyAt);
        Assert.Null(version.PromotedAt);
    }

    [Fact]
    public async Task KnownVersionKeepsItsState()
    {
        await Versions.DiscoverAsync(Version, Cancellation);
        await Database.ExecuteAsync(
            "UPDATE ddragon_version SET status = 'ready', attempts = 1",
            Cancellation);
        _time.Advance(TimeSpan.FromHours(1));

        var inserted = await Versions.DiscoverAsync(Version, Cancellation);
        await Versions.DiscoverAsync("16.18.1", Cancellation);

        Assert.False(inserted);
        var versions = await Versions.ListAsync(Cancellation);
        Assert.Equal(["16.18.1", Version], versions.Select(static version => version.Version));
        Assert.Equal(DdragonVersionStatus.Ready, versions[1].Status);
        Assert.Equal(1, versions[1].Attempts);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }

    private static string Sha(int seed) => seed.ToString("x2", null).PadLeft(64, 'e');

    private static DdragonAssetEntry Present(string key, int seed) =>
        DdragonAssetEntry.Present(Version, Type, key, Sha(seed), "png");

    private static DdragonAssetEntry Absent(string key) =>
        DdragonAssetEntry.Absent(Version, Type, key);

    // Each writer starts at another key, and every other writer goes backwards.
    private static IEnumerable<string> InWriterOrder(List<string> keys, int writer)
    {
        var start = writer * keys.Count / Writers;
        var rotated = keys.Skip(start).Concat(keys.Take(start));
        return writer % 2 == 0 ? rotated : rotated.Reverse();
    }
}
