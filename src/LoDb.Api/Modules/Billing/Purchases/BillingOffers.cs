using LoDb.Api.Modules.Billing.Catalog;

namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>What the public API sells: credit packs and subscriptions, priced in euros.</summary>
internal sealed record BillingOffers
{
    /// <summary>False while payments are closed: the portal then sells nothing.</summary>
    public required bool Available { get; init; }

    /// <summary>ISO 4217 code, lowercase as Stripe writes it: <c>eur</c>.</summary>
    public required string Currency { get; init; }

    /// <summary>The credit packs, from the smallest.</summary>
    public required IReadOnlyList<ApiPackTerms> Packs { get; init; }

    /// <summary>The subscriptions, from the cheapest.</summary>
    public required IReadOnlyList<ApiPlanTerms> Plans { get; init; }
}
