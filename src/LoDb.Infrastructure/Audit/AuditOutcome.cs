namespace LoDb.Infrastructure.Audit;

/// <summary>
/// Result of an audited action, stored as <c>success</c>, <c>failure</c> or <c>denied</c>.
/// </summary>
public enum AuditOutcome
{
    Success,

    Failure,

    /// <summary>Refused by an authorization or antiforgery check.</summary>
    Denied,
}
