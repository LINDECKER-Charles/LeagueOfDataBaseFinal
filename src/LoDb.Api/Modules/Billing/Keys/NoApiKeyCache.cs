using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.Billing.Keys;

/// <summary>
/// The cache while the public API caches nothing: registered only if no other
/// <see cref="IApiKeyCache"/> is.
/// </summary>
internal sealed class NoApiKeyCache : IApiKeyCache
{
    public void Invalidate(ApiKey key) => ArgumentNullException.ThrowIfNull(key);
}
