namespace LoDb.Api.Modules.Admin.Scaleway.Views;

/// <summary>One SKU of the project over the period, as Scaleway bills it.</summary>
internal sealed record ScalewayBillLine
{
    /// <summary>
    /// Scaleway's product (<c>Standard Multi-AZ</c>, <c>External Bandwidth</c>…).
    /// </summary>
    public required string Product { get; init; }

    /// <summary>Scaleway's resource (<c>Multi-AZ - fr-par</c>, <c>Pack</c>…).</summary>
    public required string Resource { get; init; }

    /// <summary>The quantity billed, in <c>unit</c>; negative for a deduction.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Scaleway's unit: <c>gigabyte_hour</c>, <c>gigabyte</c>, <c>plan</c>…</summary>
    public required string Unit { get; init; }

    /// <summary>Excluding VAT, to four decimals; negative for a free tier's deduction.</summary>
    public required decimal Amount { get; init; }
}
