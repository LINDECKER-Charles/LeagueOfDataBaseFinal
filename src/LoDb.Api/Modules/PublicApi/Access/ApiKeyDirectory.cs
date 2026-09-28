using System.Globalization;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Access;

/// <summary>
/// The keys of <c>/v1</c> by fingerprint, read once per <c>KeyCacheLifetime</c> and kept in
/// this instance's memory, the unknown ones included.
/// </summary>
/// <remarks>
/// Every entry is filed under the current generation: <see cref="Invalidate"/> starts a new
/// one, so the next request of every key reads it again while the entries of the previous
/// generation, out of reach, wait for their expiry. Concurrent requests of a key share one
/// read.
/// </remarks>
internal sealed class ApiKeyDirectory(
    HybridCache cache,
    ApiKeyStore store,
    UnknownKeys unknown,
    IOptions<PublicApiOptions> options) : IApiKeyCache
{
    private const string CacheKeyPrefix = "lodb:publicapi:key:";

    // In this instance only: another one sees a change within its own lifetime.
    private readonly HybridCacheEntryOptions _entry = new()
    {
        Expiration = options.Value.KeyCacheLifetime,
        LocalCacheExpiration = options.Value.KeyCacheLifetime,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    private long _generation;

    /// <summary>The key whose fingerprint is <paramref name="hash"/>, null when none is.</summary>
    /// <exception cref="Npgsql.NpgsqlException">The database could not be read.</exception>
    public async ValueTask<ApiKeySnapshot?> FindAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref _generation);
        if (unknown.Contains(hash, generation))
        {
            return null;
        }

        var cacheKey = string.Concat(
            CacheKeyPrefix,
            generation.ToString(CultureInfo.InvariantCulture),
            ":",
            hash);
        var key = await cache.GetOrCreateAsync(
            cacheKey,
            (store, hash),
            static async (state, token) => await state.store.LoadAsync(state.hash, token),
            _entry,
            cancellationToken: cancellationToken);
        if (key is null)
        {
            // Kept apart, where their number is bounded.
            await cache.RemoveAsync(cacheKey, cancellationToken);
            unknown.Add(hash, generation);
        }

        return key;
    }

    public void Invalidate(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Interlocked.Increment(ref _generation);
        unknown.Clear();
    }
}
