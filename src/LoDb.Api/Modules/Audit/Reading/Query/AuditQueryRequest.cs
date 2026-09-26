using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>The filters and the page of a journal query, all optional.</summary>
/// <param name="Action">Dotted action codes, repeated: any of them matches.</param>
/// <param name="Category">A group of actions: auth, account, build, apikey or admin.</param>
/// <param name="Outcome">success, failure or denied.</param>
/// <param name="ActorType">user, admin or anonymous.</param>
/// <param name="ActorId">Id of the acting account.</param>
/// <param name="Actor">Username of the actor at the time, whatever its case.</param>
/// <param name="From">First UTC day shown.</param>
/// <param name="To">Last UTC day shown.</param>
/// <param name="Page">The page, from 1; 1 when absent.</param>
/// <param name="PageSize">Entries per page, from 1 to 100; 40 when absent.</param>
internal sealed record AuditQueryRequest(
    [FromQuery(Name = "action")] string[]? Action,
    [FromQuery(Name = "category")] string? Category,
    [FromQuery(Name = "outcome")] string? Outcome,
    [FromQuery(Name = "actorType")] string? ActorType,
    [FromQuery(Name = "actorId")] int? ActorId,
    [FromQuery(Name = "actor")] string? Actor,
    [FromQuery(Name = "from")] DateOnly? From,
    [FromQuery(Name = "to")] DateOnly? To,
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize);
