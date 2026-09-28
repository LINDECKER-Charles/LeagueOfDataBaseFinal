using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Modules.Audit.Vocabulary;

/// <summary>
/// The codes the journal stores and its filters take: the admin front labels them, and never
/// holds its own copy of the closed sets.
/// </summary>
internal sealed record AuditVocabularyView
{
    public required IReadOnlyList<AuditActionView> Actions { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required IReadOnlyList<string> Outcomes { get; init; }

    public required IReadOnlyList<string> ActorTypes { get; init; }

    public required IReadOnlyList<string> TargetTypes { get; init; }

    public static AuditVocabularyView Instance { get; } = new()
    {
        Actions = [.. Enum.GetValues<AuditAction>().Select(static action => new AuditActionView(
            AuditVocabulary.ToText(action),
            AuditCategories.Of(action)))],
        Categories = AuditCategories.All,
        Outcomes = [.. Enum.GetValues<AuditOutcome>().Select(AuditVocabulary.ToText)],
        ActorTypes = [.. Enum.GetValues<AuditActorType>().Select(AuditVocabulary.ToText)],
        TargetTypes = [.. Enum.GetValues<AuditTargetType>().Select(AuditVocabulary.ToText)],
    };
}
