using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin.Robots;

/// <summary>The query of <c>GET /api/admin/analytics/robot-hits</c>.</summary>
internal sealed record RobotHitFilters
{
    /// <summary>A robot's key, as the report lists it.</summary>
    [FromQuery(Name = "robot")]
    public string? Robot { get; init; }

    /// <summary><c>search</c>, <c>ai</c>, <c>undeclared</c>…</summary>
    [FromQuery(Name = "category")]
    public string? Category { get; init; }

    /// <summary>The first digit of the status: 2 for <c>2xx</c>…</summary>
    [FromQuery(Name = "status")]
    public int? Status { get; init; }

    /// <summary><c>verified</c>, <c>mismatch</c>, <c>unresolved</c>, <c>unverifiable</c>.</summary>
    [FromQuery(Name = "verdict")]
    public string? Verdict { get; init; }

    /// <summary><c>detail</c>, <c>archive</c>, <c>sitemap</c>…</summary>
    [FromQuery(Name = "section")]
    public string? Section { get; init; }

    /// <summary>A text of the path and query, anywhere in it.</summary>
    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    /// <summary>The page, from 1.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }
}
