using System.Text;
using LoDb.Infrastructure.Storage.Datasets;

namespace LoDb.Infrastructure.Tests.Storage;

public sealed class DatasetStoreTests : IDisposable
{
    private const int ConcurrentWriters = 16;

    private static readonly DatasetKey Champions = new("14.1.1", "en_US", "champion");

    private readonly TemporaryStorage _storage = new();

    [Fact]
    public async Task DatasetIsWrittenUnderItsVersionLanguageAndType()
    {
        var written = await _storage.Datasets.WriteAsync(Champions, Json("{}"), Token);

        Assert.True(written);
        Assert.Equal("data/14.1.1/en_US/champion.json", Champions.RelativePath);
        var path = _storage.PathOf(Champions.RelativePath);
        Assert.Equal("{}", await File.ReadAllTextAsync(path, Token));
    }

    [Fact]
    public async Task DatasetIsReadBackAsWritten()
    {
        var key = new DatasetKey("14.1.1", "fr_FR", "championDetail/Aatrox");
        await _storage.Datasets.WriteAsync(key, Json("""{"id":"Aatrox"}"""), Token);

        await using var stream = await _storage.Datasets.OpenReadAsync(key, Token);

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.Equal("""{"id":"Aatrox"}""", await reader.ReadToEndAsync(Token));
        Assert.True(await _storage.Datasets.ExistsAsync(key, Token));
    }

    [Fact]
    public async Task AnAbsentDatasetIsAValue()
    {
        Assert.False(await _storage.Datasets.ExistsAsync(Champions, Token));
        Assert.Null(await _storage.Datasets.OpenReadAsync(Champions, Token));
    }

    [Fact]
    public async Task DatasetIsWrittenOnce()
    {
        await _storage.Datasets.WriteAsync(Champions, Json("""{"first":true}"""), Token);

        var again = await _storage.Datasets.WriteAsync(Champions, Json("{}"), Token);

        Assert.False(again);
        var path = _storage.PathOf(Champions.RelativePath);
        Assert.Equal("""{"first":true}""", await File.ReadAllTextAsync(path, Token));
    }

    [Fact]
    public async Task ConcurrentWritersOfTheSameDatasetLeaveOneCompleteFile()
    {
        var payloads = Enumerable.Range(0, ConcurrentWriters)
            .Select(writer => Json($$"""{"writer":{{writer}}}"""))
            .ToArray();

        var results = await Task.WhenAll(payloads.Select(payload =>
            Task.Run(() => _storage.Datasets.WriteAsync(Champions, payload, Token), Token)));

        Assert.Single(results, written => written);
        var winner = payloads[Array.IndexOf(results, true)];
        var path = _storage.PathOf(Champions.RelativePath);
        Assert.Equal(winner, await File.ReadAllBytesAsync(path, Token));
        Assert.Empty(_storage.StagingFiles());
    }

    public void Dispose() => _storage.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);
}
