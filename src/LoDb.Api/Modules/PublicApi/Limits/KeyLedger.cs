using LoDb.Api.Modules.PublicApi.Access;

namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// The billed requests of one key this month and its credits, as this instance counts them
/// between two reads of the key.
/// </summary>
/// <remarks>
/// <para>
/// A later read of the key (a greater <see cref="ApiKeySnapshot.Sequence"/>) brings the
/// credits of the database, and the requests <c>api_usage</c> counted when they exceed the
/// local count, which also holds the requests not written yet. A new UTC month starts at
/// zero.
/// </para>
/// <para>
/// The plan is charged under the lock: concurrent requests never overshoot the quota. The
/// credits are only a hint that spares a write once they are known to be spent; the
/// database decides.
/// </para>
/// </remarks>
internal sealed class KeyLedger
{
    private readonly Lock _gate = new();
    private long _sequence;
    private DateOnly _month;
    private long _used;
    private long _credits;

    public QuotaDecision TryCharge(ApiKeySnapshot key, DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            Sync(key, month);
            if (_used < key.MonthlyQuota)
            {
                _used++;
                return QuotaDecision.Plan;
            }

            return _credits > 0 ? QuotaDecision.NeedsCredit : QuotaDecision.Denied;
        }
    }

    /// <summary>
    /// Records the outcome of a credit taken for a request admitted with
    /// <paramref name="key"/>: the balance left, or null when none was left to take.
    /// </summary>
    public void RecordCredit(ApiKeySnapshot key, long? balance, DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            Sync(key, month);
            if (balance is not null)
            {
                _used++;
            }

            // A later read of the key knows better, a top-up for one.
            if (key.Sequence == _sequence)
            {
                _credits = balance is { } left ? Math.Min(_credits, left) : 0;
            }
        }
    }

    private void Sync(ApiKeySnapshot key, DateOnly month)
    {
        if (month != _month)
        {
            _month = month;
            _used = 0;
        }

        if (key.Sequence > _sequence)
        {
            _sequence = key.Sequence;
            _credits = key.CreditsBalance;
            if (key.Month == _month)
            {
                _used = Math.Max(_used, key.UsedThisMonth);
            }
        }
    }
}
