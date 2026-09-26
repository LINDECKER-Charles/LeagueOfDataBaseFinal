using System.Globalization;
using System.Text.Json;
using LoDb.Api.Modules.Audit.Http;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;

namespace LoDb.Api.Modules.Audit.Import;

/// <summary>
/// A line of the legacy journal (<c>AuditEvent::toArray</c>), read into an entry of
/// <c>audit_log</c>.
/// </summary>
/// <remarks>
/// <para>
/// Read as the legacy query service read it: absent and empty fields are the same, an
/// unknown outcome is a success, an unknown actor type is anonymous. A line without a time
/// or with an action outside the closed set is refused, as the legacy journal skipped it.
/// </para>
/// <para>
/// The legacy stack labelled an anonymous actor <c>anonyme</c>; the journal stores no label
/// for one. Its operator was an account-less <c>admin</c> whose id was a name: the id is kept
/// only when it is an account's.
/// </para>
/// </remarks>
internal static class LegacyAuditLine
{
    /// <summary>The entry of <paramref name="line"/>, or null when it cannot be one.</summary>
    public static AuditLogEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Read(document.RootElement)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AuditLogEntry? Read(JsonElement row)
    {
        var actionCode = Text(row, "action");
        if (!TryTime(Text(row, "at"), out var at)
            || !AuditCodes.TryParse(actionCode, AuditVocabulary.ParseAction, out var action))
        {
            return null;
        }

        var actorType = AuditCodes.OrDefault(
            Text(row, "actorType"), AuditVocabulary.ParseActorType, AuditActorType.Anonymous);
        var anonymous = actorType == AuditActorType.Anonymous;
        return new AuditLogEntry
        {
            OccurredAt = at,
            ActorType = actorType,
            ActorId = anonymous ? null : AccountId(Text(row, "actorId")),
            Actor = anonymous ? null : Clip(Text(row, "actor"), AuditLogEntry.ActorMaxLength),
            Action = action,
            Outcome = AuditCodes.OrDefault(
                Text(row, "outcome"), AuditVocabulary.ParseOutcome, AuditOutcome.Success),
            TargetType = AuditCodes.OrDefault<AuditTargetType?>(
                Text(row, "targetType"),
                static code => AuditVocabulary.ParseTargetType(code),
                null),
            TargetId = Clip(Text(row, "targetId"), AuditLogEntry.TargetIdMaxLength),
            Target = Clip(Text(row, "target"), AuditLogEntry.TargetMaxLength),
            Ip = Clip(Text(row, "ip"), AuditLogEntry.IpMaxLength),
            Route = Clip(Text(row, "route"), AuditLogEntry.RouteMaxLength),
            Meta = Meta(row),
        };
    }

    // A string or a number, as PHP may have written an id either way; null otherwise.
    private static string? Text(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static bool TryTime(string? text, out DateTimeOffset at)
    {
        var parsed = DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var value);
        at = parsed ? value.ToUniversalTime() : default;
        return parsed;
    }

    private static int? AccountId(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    // Only an object with fields: the legacy stack wrote null for no details.
    private static string? Meta(JsonElement row) =>
        row.TryGetProperty("meta", out var meta)
        && meta.ValueKind == JsonValueKind.Object
        && meta.EnumerateObject().Any()
            ? meta.GetRawText()
            : null;

    private static string? Clip(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;
}
