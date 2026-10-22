namespace LoDb.Api.Modules.Admin.Prerender.Revision.Http;

/// <summary>What the queue of the closed states holds of one epoch.</summary>
internal sealed record PrerenderRevisionEpoch
{
    public required int Epoch { get; init; }

    public required long Pending { get; init; }

    public required long Done { get; init; }

    public required long Failed { get; init; }

    public required long Skipped { get; init; }
}
