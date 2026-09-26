namespace LoDb.Api.Modules.Audit.Reading.Views;

/// <summary>A page of the journal, newest first.</summary>
/// <remarks>
/// No total, as in the legacy journal: one row read past the page tells whether another
/// follows, and a count over six months of entries is not worth its cost.
/// </remarks>
internal sealed record AuditPage
{
    public required IReadOnlyList<AuditEntryView> Items { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Whether the next page holds entries.</summary>
    public required bool HasMore { get; init; }
}
