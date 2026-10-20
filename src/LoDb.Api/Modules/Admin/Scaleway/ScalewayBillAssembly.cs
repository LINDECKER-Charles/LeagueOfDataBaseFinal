using LoDb.Api.Modules.Admin.Scaleway.Views;
using LoDb.Api.Modules.Admin.Scaleway.Wire;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// The bill of each project, from the organization's lines of each period: a pure function of
/// those lines.
/// </summary>
internal static class ScalewayBillAssembly
{
    /// <summary>
    /// Scaleway bills the organization in euros: the currency of a bill without line.
    /// </summary>
    public const string Euro = "EUR";

    private const int AmountDecimals = 4;

    /// <summary>
    /// The report of <paramref name="periods"/> (newest first), read at
    /// <paramref name="fetchedAt"/>, for the projects of <paramref name="options"/>.
    /// </summary>
    /// <exception cref="ScalewayBillingException">The lines mix currencies.</exception>
    public static ScalewayBillReport Of(
        IReadOnlyList<PeriodConsumptions> periods,
        ScalewayBillingOptions options,
        DateTimeOffset fetchedAt)
    {
        var lines = periods.SelectMany(static period => period.Lines).ToList();
        return new ScalewayBillReport
        {
            Configured = true,
            Stale = false,
            FetchedAt = fetchedAt,
            Currency = CurrencyOf(lines),
            Backups = ProjectOf(Guid.Parse(options.BackupsProjectId!), periods),
            Pages = ProjectOf(Guid.Parse(options.PagesProjectId!), periods),
        };
    }

    private static string CurrencyOf(List<ConsumptionLine> lines)
    {
        var currencies = lines.Select(static line => line.Value.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return currencies.Count switch
        {
            0 => Euro,
            1 => currencies[0],
            _ => throw new ScalewayBillingException("The bill mixes currencies."),
        };
    }

    private static ScalewayProjectBill ProjectOf(
        Guid projectId,
        IReadOnlyList<PeriodConsumptions> periods) =>
        new()
        {
            ProjectId = projectId,
            Periods = [.. periods.Select(period => PeriodOf(projectId, period))],
        };

    private static ScalewayBillPeriod PeriodOf(Guid projectId, PeriodConsumptions period)
    {
        var lines = period.Lines
            .Where(line => Guid.TryParse(line.ProjectId, out var id) && id == projectId)
            .ToList();
        return new ScalewayBillPeriod
        {
            Period = period.Period,
            Total = Round(lines.Sum(static line => line.Value.Amount)),
            Lines =
            [
                .. lines.Select(LineOf)
                    .OrderByDescending(static line => line.Amount)
                    .ThenBy(static line => line.Product, StringComparer.Ordinal)
                    .ThenBy(static line => line.Resource, StringComparer.Ordinal),
            ],
        };
    }

    private static ScalewayBillLine LineOf(ConsumptionLine line) =>
        new()
        {
            Product = line.ProductName ?? string.Empty,
            Resource = line.ResourceName ?? string.Empty,
            Quantity = line.BilledQuantity,
            Unit = line.Unit ?? string.Empty,
            Amount = Round(line.Value.Amount),
        };

    // As the estimates beside it round.
    private static decimal Round(decimal amount) => Math.Round(amount, AmountDecimals);
}
