namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>One version of a pass: a cell per locale, and its pages so far.</summary>
internal sealed record PrerenderVersionProgress
{
    public required string Version { get; init; }

    /// <summary>The lowest priority of its units: where the worker reaches it.</summary>
    public required int Rank { get; init; }

    /// <summary>
    /// One character per locale of Locales: . pending, r running, z stopped (running in a
    /// paused pass), c completed, w completed with failed pages, f failed, i interrupted
    /// (running in a pass no longer active), _ no unit.
    /// </summary>
    public required string Cells { get; init; }

    public required long PagesTotal { get; init; }

    public required long PagesWritten { get; init; }

    public required long PagesSkipped { get; init; }

    public required long PagesFailed { get; init; }

    public required long BytesWritten { get; init; }

    /// <summary>When one of its units last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
