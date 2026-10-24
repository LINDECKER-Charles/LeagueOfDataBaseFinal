namespace LoDb.Infrastructure.Robots.Detection;

/// <summary>How a hit was told to be a robot's.</summary>
public static class RobotDetections
{
    /// <summary>Its user agent says so.</summary>
    public const string Agent = "agent";

    /// <summary>A browser's user agent, at a pace no reader keeps.</summary>
    public const string Rate = "rate";
}
