namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>A plan of the public API sold as a subscription, the legacy <c>ApiPlan</c>.</summary>
internal sealed record ApiPlanTerms
{
    /// <summary>The code <c>api_keys.plan</c> stores and a session's metadata carries.</summary>
    public required string Code { get; init; }

    public required int MonthlyQuota { get; init; }

    public required int RatePerMinute { get; init; }

    /// <summary>Price of one period, in euro cents.</summary>
    public required long PriceCents { get; init; }

    /// <summary>Stripe's billing interval: <c>month</c> or <c>year</c>.</summary>
    public required string Interval { get; init; }
}
