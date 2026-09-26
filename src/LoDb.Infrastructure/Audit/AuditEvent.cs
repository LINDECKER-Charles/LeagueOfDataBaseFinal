namespace LoDb.Infrastructure.Audit;

/// <summary>One action to record in the audit journal.</summary>
public sealed record AuditEvent
{
    private static readonly IReadOnlyDictionary<string, object?> NoMeta =
        new Dictionary<string, object?>();

    public required AuditAction Action { get; init; }

    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    /// <summary>
    /// Null for the account of the current request, or <see cref="AuditActor.Anonymous"/>
    /// outside of one. Set it when the request's user is not settled yet, as on sign-in.
    /// </summary>
    public AuditActor? Actor { get; init; }

    public AuditTarget? Target { get; init; }

    /// <summary>
    /// Details of the action, with string, number, boolean or null values, stored as a JSON
    /// object.
    /// </summary>
    /// <remarks>
    /// <c>identifier</c>, the e-mail or username typed on a failed sign-in, is the only
    /// personal data allowed here: the log mirror leaves it out.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Meta { get; init; } = NoMeta;
}
