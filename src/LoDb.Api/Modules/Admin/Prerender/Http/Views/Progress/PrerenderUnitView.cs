namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>One unit of a pass, where the worker left it.</summary>
internal sealed record PrerenderUnitView
{
    public required string Version { get; init; }

    public required string Locale { get; init; }

    /// <summary>pending, running, completed or failed.</summary>
    public required string State { get; init; }

    /// <summary>Index of the next page to render.</summary>
    public required int Cursor { get; init; }

    public required int PagesTotal { get; init; }

    public required int PagesWritten { get; init; }

    public required int PagesSkipped { get; init; }

    public required int PagesFailed { get; init; }

    public required long BytesWritten { get; init; }

    /// <summary>When the worker first started it; null while pending.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>When it ended, completed or failed; null while it has not.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>Why its last page failed, or why it failed; null when nothing failed.</summary>
    public string? Failure { get; init; }
}
