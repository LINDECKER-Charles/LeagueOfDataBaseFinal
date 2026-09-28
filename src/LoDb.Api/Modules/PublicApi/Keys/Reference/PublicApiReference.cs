using LoDb.Api.Modules.Billing.Catalog;

namespace LoDb.Api.Modules.PublicApi.Keys.Reference;

/// <summary>
/// What the documentation of the public API states from the server's settings: where
/// <c>/v1</c> answers, what a key looks like, and the rights each offer gives.
/// </summary>
internal sealed record PublicApiReference
{
    /// <summary>The origin <c>/v1</c> answers on, without a trailing slash.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>What every key starts with: <c>lodb_</c>.</summary>
    public required string KeyPrefix { get; init; }

    public required FreePlanTerms FreePlan { get; init; }

    /// <summary>The least rate of a key holding credits, whatever its plan.</summary>
    public required int CreditsRatePerMinute { get; init; }

    /// <summary>The credit packs, from the smallest.</summary>
    public required IReadOnlyList<ApiPackTerms> Packs { get; init; }

    /// <summary>The subscriptions, from the cheapest.</summary>
    public required IReadOnlyList<ApiPlanTerms> Plans { get; init; }
}
