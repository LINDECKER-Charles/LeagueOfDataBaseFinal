namespace LoDb.Infrastructure.Persistence.Robots;

/// <summary>
/// A row of <c>robot_daily</c>: the hits of one UTC day that share an agent, a detection, a
/// verdict, a network, a section and a status, summed as each batch is written.
/// </summary>
/// <remarks>Kept with no end: it holds neither address nor path.</remarks>
public sealed class RobotDay
{
    /// <summary>A hit at least this slow counts in <see cref="SlowHits"/>.</summary>
    public const int SlowMilliseconds = 1000;

    public DateOnly Day { get; set; }

    public int AgentId { get; set; }

    public required string Detection { get; set; }

    public required string Verdict { get; set; }

    /// <summary>The registrable domain of the clients; empty when none was found.</summary>
    public required string Network { get; set; }

    public required string Section { get; set; }

    public short Status { get; set; }

    public long Hits { get; set; }

    public long Bytes { get; set; }

    /// <summary>The time nginx took to answer them all, summed.</summary>
    public long DurationMs { get; set; }

    public long SlowHits { get; set; }
}
