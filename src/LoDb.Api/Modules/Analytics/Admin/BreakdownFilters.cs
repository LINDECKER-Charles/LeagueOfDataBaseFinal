using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>The query of <c>GET /api/admin/analytics/breakdown</c>.</summary>
internal sealed record BreakdownFilters
{
    /// <summary><c>7d</c>, <c>30d</c> (by default), <c>90d</c> or <c>all</c>.</summary>
    [FromQuery(Name = "range")]
    public string? Range { get; init; }

    /// <summary><c>pages</c> (by default), <c>entities</c> or <c>referers</c>.</summary>
    [FromQuery(Name = "dimension")]
    public string? Dimension { get; init; }

    /// <summary>A text anywhere in the key, whatever its case.</summary>
    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    /// <summary>The page, from 1.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }
}
