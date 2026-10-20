namespace LoDb.Api.Modules.Admin.Scaleway.Wire;

/// <summary>
/// One line of <c>GET /billing/v2beta1/consumptions</c>: what one SKU of one project cost over
/// the period, excluding VAT. Lines are per project, never per bucket.
/// </summary>
internal sealed record ConsumptionLine
{
    public required ScalewayMoney Value { get; init; }

    public string? ProductName { get; init; }

    public string? ResourceName { get; init; }

    public required string ProjectId { get; init; }

    public string? Unit { get; init; }

    /// <summary>A number Scaleway writes as a string (<c>"196"</c>).</summary>
    public decimal BilledQuantity { get; init; }
}
