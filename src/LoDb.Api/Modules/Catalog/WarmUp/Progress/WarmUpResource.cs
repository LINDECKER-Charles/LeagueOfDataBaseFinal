using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Catalog.WarmUp.Progress;

/// <summary>How far the images of one resource's list have come.</summary>
internal sealed record WarmUpResource
{
    public required ResourceType Resource { get; init; }

    /// <summary>Images of the list the manifest had no verdict for; 0 while preparing.</summary>
    public required int Total { get; init; }

    public required int Settled { get; init; }

    /// <summary>Whether every image of the list has its verdict: its row reads "ready".</summary>
    public required bool Ready { get; init; }
}
