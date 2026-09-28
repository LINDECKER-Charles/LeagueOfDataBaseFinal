using System.Text;
using LoDb.Infrastructure.Storage;
using LoDb.Infrastructure.Storage.Blobs;

namespace LoDb.Infrastructure.Tests.Storage;

public sealed class BlobStoreTests : IDisposable
{
    // SHA-256 of the ASCII bytes "abc" (FIPS 180-2 test vector).
    private const string AbcSha256 =
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const int ConcurrentWriters = 32;
    private const int OneMebibyte = 1 << 20;

    private static readonly byte[] Abc = Encoding.ASCII.GetBytes("abc");

    private readonly TemporaryStorage _storage = new();

    [Fact]
    public async Task BlobIsAddressedByTheSha256OfItsBytes()
    {
        var stored = await _storage.Blobs.StoreAsync(Abc, ".PNG", Token);

        Assert.True(stored.Written);
        Assert.Equal(AbcSha256, stored.Key.Sha256);
        Assert.Equal("png", stored.Key.Extension);
        Assert.Equal($"blobs/{AbcSha256}.png", stored.Key.RelativePath);
        Assert.Equal($"/cdn/blobs/{AbcSha256}.png", stored.Key.PublicPath);
        Assert.Equal(Abc, await File.ReadAllBytesAsync(PathOf(stored), Token));
    }

    [Fact]
    public async Task IdenticalBytesAreStoredOnce()
    {
        var first = await _storage.Blobs.StoreAsync(Abc, "png", Token);
        var second = await _storage.Blobs.StoreAsync(Abc.ToArray(), ".png", Token);

        Assert.True(first.Written);
        Assert.False(second.Written);
        Assert.Equal(first.Key, second.Key);
        Assert.Single(Directory.GetFiles(_storage.PathOf(StorageLayout.BlobsDirectory)));
    }

    [Fact]
    public async Task AnExistingBlobIsNeverRewritten()
    {
        var target = _storage.PathOf($"blobs/{AbcSha256}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "already there", Token);

        var stored = await _storage.Blobs.StoreAsync(Abc, "png", Token);

        Assert.False(stored.Written);
        Assert.Equal("already there", await File.ReadAllTextAsync(target, Token));
    }

    [Fact]
    public async Task ConcurrentWritersOfTheSameBytesAllSucceed()
    {
        var content = new byte[OneMebibyte];
        Random.Shared.NextBytes(content);

        var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentWriters).Select(_ =>
            Task.Run(() => _storage.Blobs.StoreAsync(content, "png", Token), Token)));

        Assert.Single(results, result => result.Written);
        Assert.Single(results.Select(result => result.Key).Distinct());
        Assert.Equal(content, await File.ReadAllBytesAsync(PathOf(results[0]), Token));
        Assert.Empty(_storage.StagingFiles());
    }

    [Fact]
    public async Task ExistsReflectsTheStoredBlobs()
    {
        var stored = await _storage.Blobs.StoreAsync(Abc, "png", Token);
        var other = await _storage.Blobs.StoreAsync(Abc, "jpg", Token);
        File.Delete(PathOf(other));

        Assert.True(await _storage.Blobs.ExistsAsync(stored.Key, Token));
        Assert.False(await _storage.Blobs.ExistsAsync(other.Key, Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("p/ng")]
    [InlineData("../png")]
    [InlineData("png ")]
    [InlineData("p.ng")]
    [InlineData("averylongext")]
    public async Task AnInvalidExtensionIsRejected(string extension)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _storage.Blobs.StoreAsync(Abc, extension, Token));

        Assert.False(Directory.Exists(_storage.PathOf(StorageLayout.BlobsDirectory)));
    }

    public void Dispose() => _storage.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string PathOf(StoredBlob stored) =>
        _storage.PathOf(stored.Key.RelativePath);
}
