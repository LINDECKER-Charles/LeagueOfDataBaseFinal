namespace LoDb.Api.Modules.Admin.ApiClients.Views;

/// <summary>The counters of the key fleet: the active keys and what they consume.</summary>
internal sealed record ApiClientKpis
{
    public required int Active { get; init; }

    /// <summary>Requests of every key since the first day of the UTC month.</summary>
    public required long MonthRequests { get; init; }

    /// <summary>Prepaid requests left on the active keys.</summary>
    public required long Credits { get; init; }

    /// <summary>Active keys by plan, the largest first.</summary>
    public required IReadOnlyList<PlanCount> ByPlan { get; init; }
}
