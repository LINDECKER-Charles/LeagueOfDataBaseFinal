namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// The slice of a list a call asks for: the whole list, which the client filters itself, or
/// one page of it, which the server-side rendering shows first.
/// </summary>
internal sealed record PageRequest
{
    public const int DefaultSize = 50;
    public const int MaxSize = 200;

    private PageRequest(int? page, int? size)
    {
        Page = page;
        Size = size;
    }

    /// <summary>One-based page number; <see langword="null"/> for the whole list.</summary>
    public int? Page { get; }

    /// <summary>Entries per page; <see langword="null"/> for the whole list.</summary>
    public int? Size { get; }

    /// <summary>
    /// The request, or <see langword="null"/> when a bound is out of range. Neither value
    /// asks for the whole list; a page without a size takes <see cref="DefaultSize"/>.
    /// </summary>
    public static PageRequest? From(int? page, int? size)
    {
        if (page is null && size is null)
        {
            return new PageRequest(null, null);
        }

        var number = page ?? 1;
        var count = size ?? DefaultSize;
        return number >= 1 && count is >= 1 and <= MaxSize
            ? new PageRequest(number, count)
            : null;
    }

    /// <summary>The entries of the page; past the last one, none.</summary>
    public IReadOnlyList<TEntry> Slice<TEntry>(IReadOnlyList<TEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (Page is not { } page || Size is not { } size)
        {
            return entries;
        }

        var skipped = (long)(page - 1) * size;
        return skipped >= entries.Count
            ? []
            : [.. entries.Skip((int)skipped).Take(size)];
    }
}
