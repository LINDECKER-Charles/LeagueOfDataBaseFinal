namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// A row of <c>prerender_unit</c>: the pages of one (version, language) in a pass, with
/// where the worker stopped, so that a pass resumes across deploys.
/// </summary>
public sealed class PrerenderUnit
{
    /// <summary>The longest language tag, such as <c>zh-Hans</c>.</summary>
    public const int LocaleMaxLength = 16;

    /// <summary>The longest game version, as in <c>ddragon_version</c>.</summary>
    public const int VersionMaxLength = 32;

    public long PassId { get; set; }

    public required string Version { get; set; }

    public required string Locale { get; set; }

    public required PrerenderUnitState State { get; set; }

    /// <summary>Lower goes first; the operator's version order becomes this.</summary>
    public int Priority { get; set; }

    public int PagesTotal { get; set; }

    public int PagesWritten { get; set; }

    public int PagesSkipped { get; set; }

    public int PagesFailed { get; set; }

    public long BytesWritten { get; set; }

    /// <summary>Index in the version's ordered page list of the next page to render.</summary>
    public int Cursor { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public PrerenderPass? Pass { get; set; }
}
