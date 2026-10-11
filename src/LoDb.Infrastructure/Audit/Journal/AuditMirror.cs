using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Audit.Journal;

/// <summary>
/// The copy of a recorded event in the logs, named <c>audit.&lt;action&gt;</c>, so that
/// security events sit on the timeline of the other logs.
/// </summary>
/// <remarks>
/// <para>
/// Built from an allow-list, as the legacy mirror is: a field added to the journal later
/// does not leak by default. The address, the labels and what stands for the identifier
/// typed on a failed sign-in (<c>meta.identifier_digest</c>, and <c>meta.identifier</c> of
/// the older entries) stay out; the table remains the legal record.
/// </para>
/// <para>
/// Not a <c>[LoggerMessage]</c>: the event name follows the action and the fields follow
/// the meta keys, which a generated method cannot express.
/// </para>
/// </remarks>
internal static class AuditMirror
{
    private const string EventPrefix = "audit.";
    private const string MetaPrefix = "Meta.";
    // Pseudonymous at best: neither reaches the logs.
    private static readonly string[] IdentifierKeys = ["identifier", "identifier_digest"];

    public static void Write(
        ILogger logger,
        AuditLogEntry entry,
        IReadOnlyDictionary<string, object?> meta)
    {
        // Anything but a success is a warning, as in the legacy mirror.
        var level = entry.Outcome == AuditOutcome.Success ? LogLevel.Information : LogLevel.Warning;
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var name = AuditVocabulary.ToText(entry.Action);
        var outcome = AuditVocabulary.ToText(entry.Outcome);
        logger.Log(
            level,
            new EventId((int)entry.Action, EventPrefix + name),
            Fields(entry, meta),
            null,
            (_, _) => $"Audit {name}: {outcome}.");
    }

    private static List<KeyValuePair<string, object?>> Fields(
        AuditLogEntry entry,
        IReadOnlyDictionary<string, object?> meta)
    {
        var fields = new List<KeyValuePair<string, object?>>
        {
            new("ActorType", AuditVocabulary.ToText(entry.ActorType)),
            new("ActorId", entry.ActorId),
            new("Outcome", AuditVocabulary.ToText(entry.Outcome)),
            new("TargetType", entry.TargetType is { } type ? AuditVocabulary.ToText(type) : null),
            new("TargetId", entry.TargetId),
            new("Route", entry.Route),
        };
        foreach (var (key, value) in meta)
        {
            if (!IdentifierKeys.Contains(key, StringComparer.Ordinal))
            {
                fields.Add(new(MetaPrefix + key, value));
            }
        }

        fields.RemoveAll(static field => field.Value is null);
        return fields;
    }
}
