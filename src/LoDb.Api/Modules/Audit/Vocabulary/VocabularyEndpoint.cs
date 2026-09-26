using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Audit.Vocabulary;

/// <summary><c>GET /api/admin/audit/vocabulary</c>: the codes of the journal.</summary>
internal static class VocabularyEndpoint
{
    public static void Map(IEndpointRouteBuilder audit) =>
        audit.MapGet("/vocabulary", Read)
            .WithName("readAuditVocabulary")
            .WithSummary("Actions and their groups, outcomes, actor and target types.");

    private static Ok<AuditVocabularyView> Read() => TypedResults.Ok(AuditVocabularyView.Instance);
}
