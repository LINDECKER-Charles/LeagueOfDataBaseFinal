namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>
/// Where a credit grant comes from, stored as <c>purchase</c>, <c>migration</c>,
/// <c>reconciliation</c> or <c>admin</c>.
/// </summary>
public enum ApiCreditGrantSource
{
    /// <summary>A credit pack paid through Stripe, the only source with a session.</summary>
    Purchase,

    /// <summary>The balance a key held when the lot 6 migration ran, granted that day.</summary>
    Migration,

    /// <summary>
    /// A balance the live grants do not cover, as credits sold by the legacy stack during the
    /// rollback period, granted the day the expiry job finds it.
    /// </summary>
    Reconciliation,

    /// <summary>Requests an administrator credited to the key.</summary>
    Admin,
}
