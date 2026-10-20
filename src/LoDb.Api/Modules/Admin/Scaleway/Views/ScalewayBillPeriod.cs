namespace LoDb.Api.Modules.Admin.Scaleway.Views;

/// <summary>What the project was billed for one calendar month (UTC).</summary>
internal sealed record ScalewayBillPeriod
{
    /// <summary>The billing period, <c>yyyy-MM</c>.</summary>
    public required string Period { get; init; }

    /// <summary>The sum of the lines, deductions included, to four decimals.</summary>
    public required decimal Total { get; init; }

    /// <summary>Its lines, dearest first; none before the project's first consumption.</summary>
    public required IReadOnlyList<ScalewayBillLine> Lines { get; init; }
}
