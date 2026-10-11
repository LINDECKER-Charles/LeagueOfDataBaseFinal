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

    /// <summary>
    /// The only frame of a run refused before it started, nothing counted; a run that started
    /// has its frames from <see cref="WarmUpTracker"/>.
    /// </summary>
    public static WarmUpProgress Failed(IReadOnlyList<ResourceType> resources, string code)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new WarmUpProgress
        {
            Stage = WarmUpStage.Failed,
            Failure = code,
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
