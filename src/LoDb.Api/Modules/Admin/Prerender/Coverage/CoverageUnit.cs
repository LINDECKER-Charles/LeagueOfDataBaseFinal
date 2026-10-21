using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>A unit of one of an epoch's passes, as the coverage folds it.</summary>
internal sealed record CoverageUnit
{
    public required long PassId { get; init; }

    public required string Version { get; init; }

    /// <summary>The locale as the URLs spell it.</summary>
    public required string Locale { get; init; }

    public required PrerenderUnitState State { get; init; }

    public int PagesWritten { get; init; }

    public int PagesFailed { get; init; }

    public long BytesWritten { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// When it ended; null while it has not, and for a unit that ended before the column.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>Whether it ended, completed or failed.</summary>
    public bool HasEnded => State is PrerenderUnitState.Completed or PrerenderUnitState.Failed;

    /// <summary>
    /// Where it stands among the units of its (version, locale): when it ended, or its last
    /// change for a unit that ended before <c>ended_at</c> was kept.
    /// </summary>
    public DateTimeOffset Order => EndedAt ?? UpdatedAt;
}
