namespace LoDb.Api.Modules.Admin.ApiClients.Views;

/// <summary>
/// A page of the keys of the public API, newest first, with the counters of the fleet and
/// its heaviest users.
/// </summary>
internal sealed record AdminApiClientPage
{
    public required ApiClientKpis Kpis { get; init; }

    /// <summary>The keys that sent the most requests over the last thirty days.</summary>
    public required IReadOnlyList<TopConsumer> TopConsumers { get; init; }

    public required IReadOnlyList<AdminApiClientRow> Items { get; init; }

    /// <summary>Keys ever issued, revoked ones included.</summary>
    public required int Total { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int Pages { get; init; }
}
