namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>A version whose units wait, and until when they may.</summary>
internal sealed record PrerenderWaitView
{
    public required string Version { get; init; }

    /// <summary>catalog-pending, not-pinned or settling.</summary>
    public required string Reason { get; init; }

    public required DateTimeOffset Since { get; init; }

    /// <summary>When its units fail if it still waits: UnitWaits.Longest after Since.</summary>
    public required DateTimeOffset GivesUpAt { get; init; }
}
