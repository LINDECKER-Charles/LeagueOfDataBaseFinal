using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Catalog.WarmUp.Progress;

/// <summary>
/// One frame of the warm-up stream: the whole state of the run, never a delta, so a client
/// keeps the last frame it read and loses nothing to a frame it skipped.
/// </summary>
internal sealed record WarmUpProgress
{
    public required WarmUpStage Stage { get; init; }

    /// <summary>Images the run fetches, over every resource; 0 while preparing.</summary>
    public required int Total { get; init; }

    public required int Settled { get; init; }

    /// <summary>The requested resources, in the requested order.</summary>
    public required IReadOnlyList<WarmUpResource> Resources { get; init; }

    /// <summary>The image fetched last; null before the first one.</summary>
    public WarmUpLanding? Latest { get; init; }

    /// <summary>The problem code that ended a failed run, as a catalog call answers it.</summary>
    public string? Failure { get; init; }

    public static WarmUpProgress Preparing(IReadOnlyList<ResourceType> resources) =>
        Waiting(WarmUpStage.Preparing, resources);

    public static WarmUpProgress Failed(IReadOnlyList<ResourceType> resources, string code) =>
        Waiting(WarmUpStage.Failed, resources) with { Failure = code };

    private static WarmUpProgress Waiting(WarmUpStage stage, IReadOnlyList<ResourceType> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new WarmUpProgress
        {
            Stage = stage,
            Total = 0,
            Settled = 0,
            Resources =
            [
                .. resources.Select(static resource => new WarmUpResource
                {
                    Resource = resource,
                    Total = 0,
                    Settled = 0,
                    Ready = false,
                }),
            ],
        };
    }
}
