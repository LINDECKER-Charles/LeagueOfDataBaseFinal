namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>Stripe's statuses of a subscription, as the plan of its key follows them.</summary>
/// <remarks>
/// <c>incomplete</c> is neither: the first payment is still on its way, and its checkout
/// decides.
/// </remarks>
internal static class SubscriptionStatuses
{
    public const string Active = "active";
    public const string Trialing = "trialing";
    public const string PastDue = "past_due";
    public const string Unpaid = "unpaid";
    public const string Paused = "paused";
    public const string Canceled = "canceled";
    public const string IncompleteExpired = "incomplete_expired";

    /// <summary>Paid up: the key holds the plan.</summary>
    public static bool IsPaidUp(string? status) => status is Active or Trialing;

    /// <summary>Lapsed: the key is on the free plan until the subscription is paid up.</summary>
    public static bool IsLapsed(string? status) =>
        status is PastDue or Unpaid or Paused or Canceled or IncompleteExpired;
}
