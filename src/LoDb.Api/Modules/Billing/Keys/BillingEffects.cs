using LoDb.Api.Modules.PublicApi;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.Billing.Keys;

/// <summary>
/// What a change of rights does outside of its transaction: invalidating the keys it changed
/// and auditing it. Collected while the transaction runs, applied once it commits.
/// </summary>
/// <remarks>
/// Applied earlier, a key could be cached again with the rights of a transaction that then
/// rolls back, and the journal, which writes on its own connection, would record a change
/// that never happened.
/// </remarks>
internal sealed class BillingEffects(IApiKeyCache cache, IAuditLog audit)
{
    private readonly List<ApiKey> changedKeys = [];
    private readonly List<AuditEvent> auditEvents = [];

    public void KeyChanged(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        changedKeys.Add(key);
    }

    public void Audit(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        auditEvents.Add(auditEvent);
    }

    /// <summary>Drops what a rolled back transaction collected.</summary>
    public void Discard()
    {
        changedKeys.Clear();
        auditEvents.Clear();
    }

    /// <summary>Applies what a committed transaction collected, then forgets it.</summary>
    /// <remarks>
    /// Never cancelled: the change is already stored, and the journal does its best anyway.
    /// </remarks>
    public async Task ApplyAsync()
    {
        foreach (var key in changedKeys)
        {
            cache.Invalidate(key);
        }

        foreach (var auditEvent in auditEvents)
        {
            await audit.RecordAsync(auditEvent, CancellationToken.None);
        }

        Discard();
    }
}
