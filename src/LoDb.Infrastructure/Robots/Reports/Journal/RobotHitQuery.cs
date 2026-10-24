namespace LoDb.Infrastructure.Robots.Reports.Journal;

/// <summary>The filters of a page of the journal; a null filter keeps every hit.</summary>
public sealed record RobotHitQuery
{
    /// <summary>A robot's key.</summary>
    public string? Robot { get; init; }

    /// <summary>One of the categories of robots.</summary>
    public string? Category { get; init; }

    /// <summary>The first digit of the status: 2 for <c>2xx</c>…</summary>
    public int? StatusClass { get; init; }

    public string? Verdict { get; init; }

    public string? Section { get; init; }

    /// <summary>A LIKE pattern on the path and query, escaped with <c>\</c>.</summary>
    public string? TargetPattern { get; init; }

    /// <summary>The page, from 1.</summary>
    public int Page { get; init; } = 1;
}
