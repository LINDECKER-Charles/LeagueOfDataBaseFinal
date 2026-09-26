using LoDb.Infrastructure.Audit;

namespace LoDb.Infrastructure.Persistence.Audit;

/// <summary>A row of <c>audit_log</c>: one audited action.</summary>
/// <remarks>
/// No foreign key to <c>users</c>: the trail outlives the accounts it mentions, which is
/// why it keeps the labels of the time.
/// </remarks>
public sealed class AuditLogEntry
{
    // Column sizes: the journal cuts longer values rather than lose the event.
    public const int ActorMaxLength = 180;
    public const int TargetIdMaxLength = 64;
    public const int TargetMaxLength = 255;
    public const int IpMaxLength = 45;
    public const int RouteMaxLength = 255;

    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public AuditActorType ActorType { get; set; }

    public int? ActorId { get; set; }

    /// <summary>Username of the actor at the time; null for an anonymous actor.</summary>
    public string? Actor { get; set; }

    public AuditAction Action { get; set; }

    public AuditOutcome Outcome { get; set; }

    public AuditTargetType? TargetType { get; set; }

    public string? TargetId { get; set; }

    public string? Target { get; set; }

    /// <summary>Client address of the request, if any; never copied to the logs.</summary>
    public string? Ip { get; set; }

    /// <summary>Route pattern of the request, such as <c>/api/account/login</c>.</summary>
    public string? Route { get; set; }

    /// <summary>Details of the action, a JSON object; null when there are none.</summary>
    public string? Meta { get; set; }
}
