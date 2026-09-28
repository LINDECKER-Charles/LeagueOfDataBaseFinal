using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>
/// <c>POST /api/admin/analytics/rollup</c>: the legacy admin's "consolidate" button. Folds
/// the closed days left behind and today at once, then records it
/// (<c>admin.analytics_rollup</c>, with the count of days as the legacy stack did).
/// </summary>
internal sealed class RollupEndpoint(IAnalyticsRollup rollup, IAuditLog audit)
{
    private const string RolledKey = "rolled";

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost(
                "/rollup",
                static (
                    [FromServices] RollupEndpoint endpoint,
                    CancellationToken cancellationToken) =>
                    endpoint.RollupAsync(cancellationToken))
            .WithName("rollupAnalytics")
            .WithSummary("Writes the daily aggregates now, today's included.");

    public async Task<Ok<RollupReceipt>> RollupAsync(CancellationToken cancellationToken)
    {
        var closed = await rollup.RollupClosedAsync(cancellationToken);
        var today = await rollup.RollupTodayAsync(cancellationToken);
        IReadOnlyList<DateOnly> days = [.. closed, .. today];
        await audit.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.AdminAnalyticsRollup,
                Meta = new Dictionary<string, object?> { [RolledKey] = days.Count },
            },
            cancellationToken);
        return TypedResults.Ok(new RollupReceipt { Days = days });
    }
}
