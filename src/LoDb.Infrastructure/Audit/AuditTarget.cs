namespace LoDb.Infrastructure.Audit;

/// <summary>The object an audited action applies to.</summary>
/// <param name="Type">Kind of object.</param>
/// <param name="Id">Its id, as text: builds and API clients are not keyed by integers.</param>
/// <param name="Label">A readable name at the time of the action.</param>
public sealed record AuditTarget(AuditTargetType Type, string? Id, string? Label);
