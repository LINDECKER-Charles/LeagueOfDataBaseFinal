namespace LoDb.Api.Modules.Admin.Scaleway.Wire;

/// <summary>
/// An amount as Scaleway writes it (Google's <c>Money</c>): whole units and billionths, both
/// of the amount's sign. Deductions of free trials and free tiers are negative.
/// </summary>
internal sealed record ScalewayMoney
{
    private const decimal NanosPerUnit = 1_000_000_000m;

    public required string CurrencyCode { get; init; }

    public long Units { get; init; }

    public int Nanos { get; init; }

    /// <summary>The amount, exact.</summary>
    public decimal Amount => Units + (Nanos / NanosPerUnit);
}
