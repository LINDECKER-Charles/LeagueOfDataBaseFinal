namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>Body of <c>POST /api/billing/checkout/plan</c>.</summary>
internal sealed record PlanCheckoutRequest
{
    /// <summary><c>monthly</c>, <c>monthly_plus</c>, <c>annual</c> or <c>annual_plus</c>.</summary>
    public string? Plan { get; init; }

    /// <summary>Code of the locale of the portal, such as <c>fr</c>; English if unknown.</summary>
    public string? Locale { get; init; }
}
