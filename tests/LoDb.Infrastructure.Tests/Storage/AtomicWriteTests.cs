using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Infrastructure.Storage.Datasets;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Storage;

/// <summary>
/// Readers see a stored file whole or not at all, and a failed write publishes nothing.
/// </summary>
public sealed class AtomicWriteTests : IDisposable
{
    private const int LargeFileBytes = 16 << 20;
    private const int LargeFileCount = 6;

    private readonly TemporaryStorage _storage = new();

    [Fact]
    public async Task NoPartialFileIsEverVisible()
    {
        var keys = Enumerable.Range(0, LargeFileCount)
            .Select(index => new DatasetKey("14.1.1", "en_US", $"large{index}"))
            .ToArray();
        var paths = keys.Select(key => _storage.PathOf(key.RelativePath)).ToArray();
        var content = new byte[LargeFileBytes];
        Random.Shared.NextBytes(content);
        using var writing = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var reader = Task.Run(() => WatchSizes(paths, writing.Token), Token);
        foreach (var key in keys)
        {
            await _storage.Datasets.WriteAsync(key, content, Token);
        }

        await writing.CancelAsync();
        Assert.Empty(await reader);
        Assert.All(paths, path => Assert.Equal(LargeFileBytes, new FileInfo(path).Length));
        Assert.Empty(_storage.StagingFiles());
    }

    [Fact]
    public async Task ACanceledWritePublishesNothing()
    {
        var key = new DatasetKey("14.1.1", "en_US", "champion");
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _storage.Datasets.WriteAsync(key, new byte[] { 1 }, canceled.Token));

        Assert.False(File.Exists(_storage.PathOf(key.RelativePath)));
        Assert.Empty(_storage.StagingFiles());
    }

    [Fact]
    public async Task AFailedMoveLeavesNoStagingFile()
    {
        // A file where the version directory should be: the move cannot happen.
        Directory.CreateDirectory(_storage.PathOf("data"));
        await File.WriteAllTextAsync(_storage.PathOf("data/14.1.1"), "not a directory", Token);
        var key = new DatasetKey("14.1.1", "en_US", "champion");

        await Assert.ThrowsAnyAsync<IOException>(
            () => _storage.Datasets.WriteAsync(key, new byte[] { 1 }, Token));

        Assert.Empty(_storage.StagingFiles());
    }

    [Fact]
    public async Task AMissingRootIsNeverCreated()
    {
        var missing = _storage.PathOf("unmounted");
        await using var services = TemporaryStorage.Build(missing);
        var blobs = services.GetRequiredService<IBlobStore>();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => blobs.StoreAsync(new byte[] { 1 }, "png", Token));

        Assert.False(Directory.Exists(missing));
    }

    public void Dispose() => _storage.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Polls the targets until cancelled; returns every size seen that was not the full one.
    private static List<long> WatchSizes(string[] paths, CancellationToken stop)
    {
        var partial = new List<long>();
        while (!stop.IsCancellationRequested)
        {
            foreach (var path in paths)
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length != LargeFileBytes)
                {
                    partial.Add(file.Length);
                }
            }
        }

        return partial;
    }
}
