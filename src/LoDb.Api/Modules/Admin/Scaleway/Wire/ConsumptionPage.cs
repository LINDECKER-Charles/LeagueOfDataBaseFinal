namespace LoDb.Api.Modules.Admin.Scaleway.Wire;

/// <summary>
/// One page of <c>GET /billing/v2beta1/consumptions</c>. Its <c>updated_at</c> is left out:
/// Scaleway sets it to the time of the query, not to that of its amounts.
/// </summary>
internal sealed record ConsumptionPage
{
    /// <summary>The page's lines; null when Scaleway answers without its list.</summary>
    public required IReadOnlyList<ConsumptionLine>? Consumptions { get; init; }

    /// <summary>Lines of the whole list, every page included.</summary>
    public int TotalCount { get; init; }
}
