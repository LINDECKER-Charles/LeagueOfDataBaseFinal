namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>
/// The rule that ties the balance of a key to its grants: the balance belongs to the most
/// recent live grants first, so requests are spent from the oldest ones.
/// </summary>
/// <remarks>
/// Live grants are those not yet settled (<see cref="ApiCreditGrant.ExpiredAt"/> null),
/// ordered by purchase time then id. When a grant expires, what is left of it by this rule
/// comes off the balance; the order in which grants due together are settled does not
/// change the result.
/// </remarks>
public static class ApiCreditFifo
{
    /// <summary>
    /// What is left of <paramref name="grant"/> out of <paramref name="balance"/>, once the
    /// live grants newer than it have taken their share.
    /// </summary>
    public static long RemainingOf(
        ApiCreditGrant grant,
        long balance,
        IEnumerable<ApiCreditGrant> liveGrants)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(liveGrants);
        var newer = liveGrants
            .Where(other => other.ExpiredAt is null && IsNewer(other, grant))
            .Sum(static other => other.Requests);
        return Math.Clamp(balance - newer, 0, grant.Requests);
    }

    /// <summary>
    /// The part of <paramref name="balance"/> no live grant covers, which a
    /// <see cref="ApiCreditGrantSource.Reconciliation"/> grant takes; zero when covered.
    /// </summary>
    public static long Uncovered(long balance, IEnumerable<ApiCreditGrant> liveGrants)
    {
        ArgumentNullException.ThrowIfNull(liveGrants);
        var covered = liveGrants
            .Where(static grant => grant.ExpiredAt is null)
            .Sum(static grant => grant.Requests);
        return Math.Max(balance - covered, 0);
    }

    private static bool IsNewer(ApiCreditGrant other, ApiCreditGrant grant) =>
        other.PurchasedAt > grant.PurchasedAt
        || (other.PurchasedAt == grant.PurchasedAt && other.Id > grant.Id);
}
