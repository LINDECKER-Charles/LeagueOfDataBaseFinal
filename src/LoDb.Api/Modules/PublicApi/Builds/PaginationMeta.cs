using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Builds;

/// <summary>Where a page stands in its collection.</summary>
/// <param name="Page">The page asked for, beyond the last one included.</param>
/// <param name="PerPage">Entries per page, once capped.</param>
/// <param name="Total">Entries of the whole collection.</param>
/// <param name="TotalPages">Pages of the collection, one at least.</param>
internal sealed record PaginationMeta(
    [property: JsonPropertyName("page")] long Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("total_pages")] long TotalPages);
