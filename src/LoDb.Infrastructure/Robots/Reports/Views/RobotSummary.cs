namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>
/// One robot over a period: who it is, how much it asked and how it was answered.
/// </summary>
public sealed record RobotSummary
{
    /// <summary>
    /// Its key, safe in a URL: <c>googlebot</c>, <c>undeclared:example.net</c>.
    /// </summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Who runs it; for an undeclared robot, the network it came from.</summary>
    public required string Operator { get; init; }

    public required string Category { get; init; }

    /// <summary>
    /// Whether it explores the site on its own, rather than at a person's request.
    /// </summary>
    public required bool IsCrawler { get; init; }

    /// <summary>Its share of the robots' hits of the period, in percent.</summary>
    public required double Pct { get; init; }

    public required RobotFigures Figures { get; init; }

    public required DateOnly FirstDay { get; init; }

    public required DateOnly LastDay { get; init; }

    /// <summary>What it asked for the most: <c>detail</c>, <c>archive</c>…</summary>
    public required string TopSection { get; init; }

    /// <summary>The networks its hits came from, most frequent first, three at most.</summary>
    public required IReadOnlyList<string> Networks { get; init; }
}
