using System.Text.Json;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audit.Journal;

/// <summary>
/// Writes each event to <c>audit_log</c> with a context of its own, then mirrors it in the
/// logs.
/// </summary>
/// <remarks>
/// A context of its own, so that the event is recorded whatever becomes of the caller's
/// unit of work, which it never saves. A failed write is logged at Critical with the action
/// alone, since the journal is legally retained and nothing repairs it, then swallowed.
/// </remarks>
internal sealed partial class AuditLog(
    IDbContextFactory<LoDbDbContext> contexts,
    IHttpContextAccessor requests,
    IOptions<IdentityOptions> identity,
    TimeProvider timeProvider,
    ILogger<AuditLog> logger) : IAuditLog
{
    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var entry = await TryWriteAsync(auditEvent);
        if (entry is not null)
        {
            AuditMirror.Write(logger, entry, auditEvent.Meta);
        }
    }

    private async Task<AuditLogEntry?> TryWriteAsync(AuditEvent auditEvent)
    {
        try
        {
            var entry = CreateEntry(auditEvent);
            await using var context = await contexts.CreateDbContextAsync(CancellationToken.None);
            context.AuditLog.Add(entry);
            await context.SaveChangesAsync(CancellationToken.None);
            return entry;
        }
        catch (Exception exception)
        {
            // Named whatever the log level: a lookup, on a path that has already failed.
            var action = ActionName(auditEvent.Action);
            LogWriteFailed(logger, action, exception);
            return null;
        }
    }

    private AuditLogEntry CreateEntry(AuditEvent auditEvent)
    {
        var request = requests.HttpContext;
        var actor = auditEvent.Actor
            ?? AuditRequest.Actor(request, identity.Value.ClaimsIdentity);
        return new AuditLogEntry
        {
            OccurredAt = timeProvider.GetUtcNow(),
            ActorType = actor.Type,
            ActorId = actor.Id,
            Actor = Clip(actor.Label, AuditLogEntry.ActorMaxLength),
            Action = auditEvent.Action,
            Outcome = auditEvent.Outcome,
            TargetType = auditEvent.Target?.Type,
            TargetId = Clip(auditEvent.Target?.Id, AuditLogEntry.TargetIdMaxLength),
            Target = Clip(auditEvent.Target?.Label, AuditLogEntry.TargetMaxLength),
            Ip = Clip(AuditRequest.Ip(request), AuditLogEntry.IpMaxLength),
            Route = Clip(AuditRequest.Route(request), AuditLogEntry.RouteMaxLength),
            Meta = auditEvent.Meta.Count == 0 ? null : JsonSerializer.Serialize(auditEvent.Meta),
        };
    }

    private static string? Clip(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;

    // An action outside the closed set is logged by its number rather than lost with it.
    private static string ActionName(AuditAction action) =>
        Enum.IsDefined(action) ? AuditVocabulary.ToText(action) : action.ToString();

    [LoggerMessage(
        EventName = "audit.journal.write_failed",
        Level = LogLevel.Critical,
        Message = "Audit event {Action} was not recorded.")]
    private static partial void LogWriteFailed(ILogger logger, string action, Exception exception);
}
