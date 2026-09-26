using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Audit.Reading.Query;
using LoDb.Api.Modules.Audit.Reading.Views;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>
/// <c>GET /api/admin/audit</c>: the journal, newest first, filtered by action, group,
/// outcome, actor and period.
/// </summary>
internal static class JournalEndpoint
{
    public static void Map(IEndpointRouteBuilder audit) =>
        audit.MapGet("/", ReadAsync)
            .WithName("readAuditJournal")
            .WithSummary("A page of the audit journal, newest first, filtered.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<AuditPage>, AccountProblem>> ReadAsync(
        [AsParameters] AuditQueryRequest request,
        [FromServices] AuditReader reader,
        CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        return AuditQueryRules.Parse(request, errors) is { } query
            ? TypedResults.Ok(await reader.JournalAsync(query, cancellationToken))
            : errors.ToProblem();
    }
}
