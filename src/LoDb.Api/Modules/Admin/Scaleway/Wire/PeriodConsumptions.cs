namespace LoDb.Api.Modules.Admin.Scaleway.Wire;

/// <summary>Every line of a billing period, all pages read.</summary>
/// <param name="Period">The billing period, <c>yyyy-MM</c>.</param>
/// <param name="Lines">The lines of every project of the organization.</param>
internal sealed record PeriodConsumptions(string Period, IReadOnlyList<ConsumptionLine> Lines);
