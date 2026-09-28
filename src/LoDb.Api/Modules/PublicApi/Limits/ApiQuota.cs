using System.Collections.Concurrent;
using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Metering;

namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// Pays for the billed requests of <c>/v1</c>: the monthly quota first, then the prepaid
/// credits, otherwise refused.
/// </summary>
/// <remarks>
/// One ledger per key admitted by this instance, kept across the reloads of the key so that
/// the requests not yet written to <c>api_usage</c> still count.
/// </remarks>
internal sealed class ApiQuota(UsageCalendar calendar)
{
    private readonly ConcurrentDictionary<int, KeyLedger> _ledgers = new();

    public QuotaDecision TryCharge(ApiKeySnapshot key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return LedgerOf(key).TryCharge(key, calendar.CurrentMonth);
    }

    /// <inheritdoc cref="KeyLedger.RecordCredit"/>
    public void RecordCredit(ApiKeySnapshot key, long? balance)
    {
        ArgumentNullException.ThrowIfNull(key);
        LedgerOf(key).RecordCredit(key, balance, calendar.CurrentMonth);
    }

    private KeyLedger LedgerOf(ApiKeySnapshot key) =>
        _ledgers.GetOrAdd(key.Id, static _ => new KeyLedger());
}
