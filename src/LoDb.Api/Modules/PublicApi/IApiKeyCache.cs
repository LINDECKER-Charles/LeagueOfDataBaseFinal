using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// What <c>/v1</c> keeps in memory of the API keys: their rights (plan, quota, credits,
/// rate limit, revocation) and their consumption of the month.
/// </summary>
/// <remarks>
/// Whoever changes a key in the database (revocation, regeneration, credits, plan: the
/// portal, the Stripe webhooks, the admin) calls <see cref="Invalidate"/> once the change is
/// committed, and the next request reads the key again: the change applies at once on this
/// instance. Another instance, or a change made outside the API, shows within
/// <c>LoDb:PublicApi:KeyCacheLifetime</c>.
/// </remarks>
internal interface IApiKeyCache
{
    /// <summary>
    /// Forgets every key held, <paramref name="key"/> included: a regenerated key changes
    /// hash, and a new one may have been refused while it did not exist yet. Changes are
    /// rare, a reload costs one query per key.
    /// </summary>
    void Invalidate(ApiKey key);
}
