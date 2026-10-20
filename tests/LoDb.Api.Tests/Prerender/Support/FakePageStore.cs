using System.Collections.Concurrent;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;

namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>
/// A page store in memory: what was written, in order, and the failures the test scripts, by
/// key prefix, one write at a time.
/// </summary>
internal sealed class FakePageStore : IPageStore
{
    private readonly ConcurrentDictionary<string, PageObject> _objects = new();
    private readonly ConcurrentQueue<string> _writes = new();
    private readonly ConcurrentQueue<(string Prefix, PageStoreException Failure)> _failures = new();

    /// <summary>The keys written, in order; a create-only write of a taken key is not.</summary>
    public IReadOnlyCollection<string> Writes => _writes;

    public IReadOnlyDictionary<string, PageObject> Objects => _objects;

    /// <summary>
    /// The next write under <paramref name="prefix"/> throws <paramref name="failure"/>.
    /// </summary>
    public void FailNext(string prefix, PageStoreException failure) =>
        _failures.Enqueue((prefix, failure));

    public Task<PageWriteOutcome> PutAsync(PageObject page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_failures.TryPeek(out var next)
            && page.Key.StartsWith(next.Prefix, StringComparison.Ordinal)
            && _failures.TryDequeue(out _))
        {
            throw next.Failure;
        }

        if (page.IsCreateOnly && !_objects.TryAdd(page.Key, page))
        {
            return Task.FromResult(PageWriteOutcome.AlreadyPresent);
        }

        _objects[page.Key] = page;
        _writes.Enqueue(page.Key);
        return Task.FromResult(PageWriteOutcome.Written);
    }

    public Task<StoredPage?> HeadAsync(string key, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The pass never reads the store.");
}
