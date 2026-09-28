using System.Text;
using LoDb.Infrastructure.Storage.Blobs;

namespace LoDb.Infrastructure.Tests.Storage;

public sealed class WebpSiblingTests : IDisposable
{
    private static readonly byte[] Png = Encoding.ASCII.GetBytes("png bytes");
    private static readonly byte[] Webp = Encoding.ASCII.GetBytes("webp bytes");

    private readonly TemporaryStorage _storage = new();

    [Theory]
    [InlineData("/cdn/blobs/abc.png", "/cdn/blobs/abc.webp")]
    [InlineData("blobs/abc.png", "blobs/abc.webp")]
    [InlineData("abc.png", "abc.webp")]
    [InlineData("/cdn/blobs/abc.svg", null)]
    [InlineData("/cdn/blobs/abc.jpg", null)]
    [InlineData("/cdn/blobs/abc.webp", null)]
    [InlineData("/cdn/blobs/abc.PNG", null)]
    [InlineData("/cdn/blobs/abc", null)]
    public void SiblingIsDerivedForPngPathsOnly(string path, string? expected) =>
        Assert.Equal(expected, WebpSibling.Of(path));

    [Fact]
    public async Task SiblingIsWrittenNextToThePngBlob()
    {
        var blob = await _storage.Blobs.StoreAsync(Png, "png", Token);

        var written = await _storage.Blobs.StoreWebpSiblingAsync(blob.Key, Webp, Token);

        Assert.True(written);
        Assert.True(blob.Key.HasWebpSlot);
        Assert.Equal($"blobs/{blob.Key.Sha256}.webp", blob.Key.WebpRelativePath);
        Assert.Equal($"/cdn/blobs/{blob.Key.Sha256}.webp", blob.Key.WebpPublicPath);
        var siblingPath = _storage.PathOf(blob.Key.WebpRelativePath!);
        Assert.Equal(Webp, await File.ReadAllBytesAsync(siblingPath, Token));
        Assert.True(await _storage.Blobs.HasWebpSiblingAsync(blob.Key, Token));
    }

    [Fact]
    public async Task SiblingIsWrittenOnce()
    {
        var blob = await _storage.Blobs.StoreAsync(Png, "png", Token);
        Assert.False(await _storage.Blobs.HasWebpSiblingAsync(blob.Key, Token));
        await _storage.Blobs.StoreWebpSiblingAsync(blob.Key, Webp, Token);

        var again = await _storage.Blobs.StoreWebpSiblingAsync(blob.Key, Png, Token);

        Assert.False(again);
        var siblingPath = _storage.PathOf(blob.Key.WebpRelativePath!);
        Assert.Equal(Webp, await File.ReadAllBytesAsync(siblingPath, Token));
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("jpg")]
    [InlineData("webp")]
    public async Task ABlobWithoutWebpSlotHasNoSibling(string extension)
    {
        var blob = await _storage.Blobs.StoreAsync(Png, extension, Token);

        Assert.False(blob.Key.HasWebpSlot);
        Assert.Null(blob.Key.WebpPublicPath);
        Assert.False(await _storage.Blobs.HasWebpSiblingAsync(blob.Key, Token));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _storage.Blobs.StoreWebpSiblingAsync(blob.Key, Webp, Token));
    }

    public void Dispose() => _storage.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
