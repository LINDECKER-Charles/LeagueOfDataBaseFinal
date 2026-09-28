using System.Collections.Concurrent;
using LoDb.Api.Modules.PublicApi;
using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Tests.PublicApiKeys.Support;

/// <summary>
/// The key cache of <c>/v1</c> as it is, which also keeps what it is told to forget.
/// </summary>
public sealed class SpyKeyCache : IApiKeyCache
{
    private readonly IApiKeyCache _inner;
    private readonly ConcurrentQueue<int> _invalidated = new();

    internal SpyKeyCache(IApiKeyCache inner) => _inner = inner;

    /// <summary>The ids of the keys invalidated, in order.</summary>
    public IReadOnlyList<int> Invalidated => [.. _invalidated];

    public void Invalidate(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _invalidated.Enqueue(key.Id);
        _inner.Invalidate(key);
    }
}
