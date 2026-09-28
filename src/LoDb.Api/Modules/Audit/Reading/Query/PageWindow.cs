namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>The slice of the journal a request asks for.</summary>
/// <param name="Page">The page, from 1.</param>
/// <param name="PageSize">Entries per page, from 1 to <see cref="MaxPageSize"/>.</param>
internal sealed record PageWindow(int Page, int PageSize)
{
    /// <summary>The legacy journal showed 40 entries a page.</summary>
    public const int DefaultPageSize = 40;

    public const int MaxPageSize = 100;

    public int Offset => (Page - 1) * PageSize;
}
