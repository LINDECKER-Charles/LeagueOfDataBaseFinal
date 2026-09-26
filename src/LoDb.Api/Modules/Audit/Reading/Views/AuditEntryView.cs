using System.Text.Json;
using LoDb.Api.Modules.Audit.Vocabulary;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;

namespace LoDb.Api.Modules.Audit.Reading.Views;

/// <summary>One entry of the audit journal, as the admin reads it.</summary>
/// <remarks>
/// Codes are the stored ones (<c>user.login</c>, <c>api_client</c>), those of the legacy
/// journal and of <c>GET /api/admin/audit/vocabulary</c>; the admin front labels them.
/// </remarks>
internal sealed record AuditEntryView
{
    public required long Id { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary><c>user</c>, <c>admin</c> or <c>anonymous</c>.</summary>
    public required string ActorType { get; init; }

    /// <summary>Id of the acting account; null for an anonymous actor.</summary>
    public int? ActorId { get; init; }

    /// <summary>Username of the actor at the time.</summary>
    public string? Actor { get; init; }

    /// <summary>Dotted action code, such as <c>user.login</c>.</summary>
    public required string Action { get; init; }

    /// <summary>Group of the action: auth, account, build, apikey or admin.</summary>
    public required string Category { get; init; }

    /// <summary><c>success</c>, <c>failure</c> or <c>denied</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary><c>user</c>, <c>build</c>, <c>apikey</c> or <c>api_client</c>.</summary>
    public string? TargetType { get; init; }

    public string? TargetId { get; init; }

    /// <summary>Readable name of the target at the time.</summary>
    public string? Target { get; init; }

    /// <summary>Client address of the request, as the legacy admin showed it.</summary>
    public string? Ip { get; init; }

    /// <summary>
    /// Route of the request: a pattern, or a legacy route name for imported entries.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>Details of the action; null when there are none.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Meta { get; init; }

    public static AuditEntryView Of(AuditLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditEntryView
        {
            Id = entry.Id,
            OccurredAt = entry.OccurredAt,
            ActorType = AuditVocabulary.ToText(entry.ActorType),
            ActorId = entry.ActorId,
            Actor = entry.Actor,
            Action = AuditVocabulary.ToText(entry.Action),
            Category = AuditCategories.Of(entry.Action),
            Outcome = AuditVocabulary.ToText(entry.Outcome),
            TargetType = entry.TargetType is { } type ? AuditVocabulary.ToText(type) : null,
            TargetId = entry.TargetId,
            Target = entry.Target,
            Ip = entry.Ip,
            Route = entry.Route,
            Meta = entry.Meta is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Meta),
        };
    }
}
