using System.Collections.Concurrent;
using LoDb.Api.Modules.PublicApi;
using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>The cache of the public API's keys as a double: it keeps what it forgets.</summary>
public sealed class RecordingKeyCache : IApiKeyCache
{
    private readonly ConcurrentQueue<int> _invalidated = new();

    /// <summary>The ids of the keys invalidated, in order.</summary>
    public IReadOnlyList<int> Invalidated => [.. _invalidated];

    public void Invalidate(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _invalidated.Enqueue(key.Id);
    }
}
