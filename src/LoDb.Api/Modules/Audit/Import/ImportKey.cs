using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;

namespace LoDb.Api.Modules.Audit.Import;

/// <summary>
/// What tells an imported entry from another: an import run again finds its entries by it
/// and adds none twice.
/// </summary>
internal readonly record struct ImportKey(
    long UtcTicks,
    AuditAction Action,
    AuditActorType ActorType,
    int? ActorId,
    AuditTargetType? TargetType,
    string? TargetId,
    string? Route)
{
    public static ImportKey Of(AuditLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new ImportKey(
            entry.OccurredAt.UtcTicks,
            entry.Action,
            entry.ActorType,
            entry.ActorId,
            entry.TargetType,
            entry.TargetId,
            entry.Route);
    }
}
