namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>A line of a chart: its name and a value for each date of the report.</summary>
public sealed record RobotSeries
{
    /// <summary>A category, or a class of statuses (<c>2xx</c>…).</summary>
    public required string Name { get; init; }

    public required IReadOnlyList<long> Values { get; init; }
}
