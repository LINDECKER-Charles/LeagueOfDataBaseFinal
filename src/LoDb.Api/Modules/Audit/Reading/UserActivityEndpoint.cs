using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Audit.Reading.Query;
using LoDb.Api.Modules.Audit.Reading.Views;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>
/// <c>GET /api/admin/audit/users/{userId}</c>: every action of one account, the filters of
/// the journal applying on top.
/// </summary>
/// <remarks>
/// A deleted account still has its trail, read by id: only the entries naming it by its
/// e-mail or username need the account itself.
/// </remarks>
internal static class UserActivityEndpoint
{
    public static void Map(IEndpointRouteBuilder audit) =>
        audit.MapGet("/users/{userId:int}", ReadAsync)
            .WithName("readUserAuditActivity")
            .WithSummary("A page of what one account did or underwent, newest first.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<UserActivity>, AccountProblem>> ReadAsync(
        [FromRoute] int userId,
        [AsParameters] AuditQueryRequest request,
        [FromServices] AuditReader reader,
        CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        if (AuditQueryRules.Parse(request, errors) is not { } query)
        {
            return errors.ToProblem();
        }

        var stored = await reader.SubjectAsync(userId, cancellationToken);
        var subject = stored ?? new AuditSubject(userId, null, null);
        var page = await reader.ActivityAsync(subject, query, cancellationToken);
        return TypedResults.Ok(new UserActivity
        {
            Subject = stored is null ? null : AuditSubjectView.Of(stored),
            Activity = page,
        });
    }
}
