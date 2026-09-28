using LoDb.Api.Modules.Audit.Reading.Views;
using LoDb.Api.Modules.Audit.Retention;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>
/// <c>GET /api/admin/audit/volume</c>: the size of the journal and its retention cutoff, for
/// the purge screen.
/// </summary>
internal static class VolumeEndpoint
{
    public static void Map(IEndpointRouteBuilder audit) =>
        audit.MapGet("/volume", ReadAsync)
            .WithName("readAuditVolume")
            .WithSummary("How many entries the journal holds, their span and size.");

    private static async Task<Ok<AuditVolume>> ReadAsync(
        [FromServices] AuditReader reader,
        [FromServices] AuditRetention retention,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await reader.VolumeAsync(retention.Cutoff, cancellationToken));
}
