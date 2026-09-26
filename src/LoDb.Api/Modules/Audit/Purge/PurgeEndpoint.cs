using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Audit.Retention;
using LoDb.Infrastructure.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Audit.Purge;

/// <summary>
/// <c>POST /api/admin/audit/purge</c>: deletes a period of the journal on an operator's
/// demand, then records the purge itself (<c>admin.logs_purge</c>), which therefore survives
/// even a purge of everything.
/// </summary>
internal sealed class PurgeEndpoint(AuditRetention retention, IAuditLog audit)
{
    public const string InvalidScope = "invalid-scope";

    private const string DateFormat = "yyyy-MM-dd";
    private const string ScopeKey = "scope";
    private const string EntriesKey = "entries";
    private const string BeforeKey = "before";

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost(
                "/purge",
                static (
                    [FromBody] PurgeRequest request,
                    [FromServices] PurgeEndpoint endpoint,
                    CancellationToken cancellationToken) =>
                    endpoint.PurgeAsync(request, cancellationToken))
            .WithName("purgeAuditJournal")
            .WithSummary("Deletes the journal before a day, past the retention, or whole.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    public async Task<Results<Ok<PurgeReceipt>, AccountProblem>> PurgeAsync(
        PurgeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new FieldErrors();
        var before = Bounds(request, errors);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var deleted = await retention.DeleteBeforeAsync(before, cancellationToken);
        var scope = request.Scope!;
        await audit.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.AdminLogsPurge,
                Meta = new Dictionary<string, object?>
                {
                    [ScopeKey] = scope,
                    [EntriesKey] = deleted,
                    [BeforeKey] = before?.ToString(DateFormat, CultureInfo.InvariantCulture),
                },
            },
            cancellationToken);
        return TypedResults.Ok(
            new PurgeReceipt { Scope = scope, Before = before, Deleted = deleted });
    }

    // The first instant kept; null for everything. Null with an error when invalid.
    private DateTimeOffset? Bounds(PurgeRequest request, FieldErrors errors)
    {
        switch (request.Scope)
        {
            case PurgeScopes.All:
                return null;
            case PurgeScopes.Retention:
                return retention.Cutoff;
            case PurgeScopes.Before when request.Before is { } day:
                return new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            case PurgeScopes.Before:
                errors.Add(BeforeKey, FieldErrors.Required);
                return null;
            case null or "":
                errors.Add(ScopeKey, FieldErrors.Required);
                return null;
            default:
                errors.Add(ScopeKey, InvalidScope);
                return null;
        }
    }
}
