using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.Billing.Keys;

/// <summary>
/// The cache of the keys the public API resolves (L6.3): whoever changes the rights of a key
/// invalidates it, so that the change applies at the next request.
/// </summary>
/// <remarks>
/// Declared here for the payments while the public API does not expose its own: once it
/// does, the payments call that one and this interface goes, along with
/// <see cref="NoApiKeyCache"/>.
/// </remarks>
internal interface IApiKeyCache
{
    /// <summary>Forgets what is cached of <paramref name="key"/>, by its id or hash.</summary>
    void Invalidate(ApiKey key);
}
