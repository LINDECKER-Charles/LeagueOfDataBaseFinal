using System.Globalization;
using LoDb.Api.Modules.Billing.Checkout;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>What a completed session tells, read as the legacy handler reads it.</summary>
internal static class SessionFields
{
    /// <summary>The value of the metadata <paramref name="key"/>; null when absent.</summary>
    public static string? Metadata(Session session, string key)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Metadata is { } metadata && metadata.TryGetValue(key, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// The buyer of a purchase: the metadata <c>user_id</c>, else the client reference; 0,
    /// which no account has, when neither is a number.
    /// </summary>
    public static int BuyerId(Session session) =>
        Number(Metadata(session, CheckoutMetadata.UserId) ?? session.ClientReferenceId) ?? 0;

    /// <summary>The signed-in donor, whom the session names by its client reference.</summary>
    public static int? DonorId(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Number(session.ClientReferenceId);
    }

    /// <summary>The requests of a pack; 0 when the metadata holds no positive number.</summary>
    public static long Requests(Session session) =>
        long.TryParse(
            Metadata(session, CheckoutMetadata.Requests),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var requests)
            ? requests
            : 0;

    private static int? Number(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
