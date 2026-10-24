namespace LoDb.Infrastructure.Persistence.Robots;

/// <summary>
/// A row of <c>robot_hit</c>: one request of a robot as nginx answered it, kept
/// <c>LoDb:Robots:HitRetention</c> (30 days) for the paths and the latest hits.
/// </summary>
/// <remarks>
/// Neither the client's address nor its host name is kept: only the registrable domain of its
/// reverse DNS name (<see cref="Network"/>) and the verdict drawn from it. The daily
/// aggregates (<see cref="RobotDay"/>) outlive the hits.
/// </remarks>
public sealed class RobotHit
{
    public const int NetworkMaxLength = 253;
    public const int MethodMaxLength = 16;
    public const int TargetMaxLength = 1024;
    public const int LabelMaxLength = 16;

    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public int AgentId { get; set; }

    /// <summary><c>agent</c>: it said it is a robot; <c>rate</c>: its pace gave it away.</summary>
    public required string Detection { get; set; }

    /// <summary>
    /// <c>verified</c>, <c>mismatch</c>, <c>unresolved</c> or <c>unverifiable</c>.
    /// </summary>
    public required string Verdict { get; set; }

    /// <summary>
    /// Registrable domain of the client's reverse DNS name, such as <c>googlebot.com</c>.
    /// </summary>
    public string? Network { get; set; }

    public required string Method { get; set; }

    /// <summary>The path and query requested.</summary>
    public required string Target { get; set; }

    /// <summary>What was requested: <c>detail</c>, <c>archive</c>, <c>sitemap</c>…</summary>
    public required string Section { get; set; }

    public short Status { get; set; }

    public int DurationMs { get; set; }

    public long Bytes { get; set; }

    /// <summary>nginx's cache status, such as <c>HIT</c>; null when no cache took part.</summary>
    public string? Cache { get; set; }
}
