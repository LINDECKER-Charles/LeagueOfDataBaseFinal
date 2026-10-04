namespace LoDb.Infrastructure.Audit;

/// <summary>The identity an audited action is attributed to.</summary>
/// <param name="Type">Kind of actor.</param>
/// <param name="Id">Id of the account; null for <see cref="AuditActorType.Anonymous"/>.</param>
/// <param name="Label">Username at the time of the action, kept if the account goes.</param>
public sealed record AuditActor(AuditActorType Type, int? Id, string? Label)
{
    public static AuditActor Anonymous { get; } = new(AuditActorType.Anonymous, null, null);

    public static AuditActor User(int id, string? label) => new(AuditActorType.User, id, label);

    public static AuditActor Admin(int id, string? label) => new(AuditActorType.Admin, id, label);
}
