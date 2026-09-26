namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>
/// The metadata a Checkout session carries to its webhook, as the legacy stack writes it: its
/// <c>kind</c> routes the completed session.
/// </summary>
internal static class CheckoutMetadata
{
    public const string Kind = "kind";
    public const string Source = "source";
    public const string UserId = "user_id";
    public const string Requests = "requests";
    public const string Plan = "plan";

    /// <summary>A donation; a session without any kind, older than them, is one too.</summary>
    public const string DonationKind = "donation";

    public const string PackKind = "api_pack";
    public const string PlanKind = "api_plan";

    /// <summary>The <c>source</c> of a donation session.</summary>
    public const string DonationSource = "lodb-donate";
}
