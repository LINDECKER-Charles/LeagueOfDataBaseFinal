namespace LoDb.Infrastructure.Audit;

/// <summary>
/// The audit journal (<c>audit_log</c>), written by every module, with a copy of each event
/// in the logs (<c>audit.&lt;action&gt;</c>).
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Records <paramref name="auditEvent"/>, best effort: the audited action never fails
    /// because of its journal. A lost event is logged as <c>audit.journal.write_failed</c>.
    /// </summary>
    /// <param name="auditEvent">The action to record.</param>
    /// <param name="cancellationToken">
    /// Not observed by the write: an action that happened keeps its trail even when its
    /// request is aborted. The database command timeout bounds the wait.
    /// </param>
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
