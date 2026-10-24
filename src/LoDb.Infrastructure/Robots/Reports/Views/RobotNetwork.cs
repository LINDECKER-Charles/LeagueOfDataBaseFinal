namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>
/// A network robots came from: the registrable domain their addresses reverse to and back.
/// </summary>
public sealed record RobotNetwork
{
    /// <summary>Such as <c>googlebot.com</c>, <c>amazonaws.com</c>.</summary>
    public required string Domain { get; init; }

    public required long Hits { get; init; }

    /// <summary>Distinct robots seen from it.</summary>
    public required int Robots { get; init; }

    public required double Pct { get; init; }
}
