using System.Globalization;
using LoDb.Api.Modules.PublicApi.Http;

namespace LoDb.Api.Modules.PublicApi.Builds;

/// <summary>
/// A page of a collection of <c>/v1</c>, read from <c>?page=</c> and <c>?per_page=</c> as
/// go-api reads them.
/// </summary>
/// <remarks>
/// Each is a positive whole number, an optional sign and leading zeros allowed; an empty or
/// missing one takes its default. A page size above <see cref="MaxPerPage"/> is capped
/// rather than refused.
/// </remarks>
/// <param name="Page">The page, from 1.</param>
/// <param name="PerPage">Entries per page, from 1 to <see cref="MaxPerPage"/>.</param>
internal readonly record struct Pagination(long Page, int PerPage)
{
    public const string PageParameter = "page";
    public const string PerPageParameter = "per_page";
    public const int DefaultPerPage = 20;
    public const int MaxPerPage = 50;

    private const long FirstPage = 1;

    public static V1Error Invalid { get; } = new(
        StatusCodes.Status400BadRequest,
        V1Errors.InvalidRequest,
        "page and per_page must be positive integers");

    /// <summary>Entries to skip: meaningful for a page within <see cref="TotalPages"/>.</summary>
    public long Offset => (Page - FirstPage) * PerPage;

    public static bool TryRead(QueryString query, out Pagination pagination)
    {
        pagination = default;
        var pageText = GoQuery.Get(query, PageParameter);
        var perPageText = GoQuery.Get(query, PerPageParameter);
        if (!TryReadPositive(pageText, FirstPage, out var page)
            || !TryReadPositive(perPageText, DefaultPerPage, out var perPage))
        {
            return false;
        }

        pagination = new Pagination(page, (int)Math.Min(perPage, MaxPerPage));
        return true;
    }

    /// <summary>Pages of <paramref name="total"/> entries: one at least, empty or not.</summary>
    public long TotalPages(long total) => total <= 0 ? 1 : ((total - 1) / PerPage) + 1;

    // strconv.Atoi: an optional sign, then decimal digits, within 64 bits.
    private static bool TryReadPositive(string text, long fallback, out long value)
    {
        if (text.Length == 0)
        {
            value = fallback;
            return true;
        }

        return long.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value)
            && value >= 1;
    }
}
