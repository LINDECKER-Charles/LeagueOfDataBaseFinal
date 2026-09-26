using LoDb.Api.Modules.Admin.Monitoring.Views;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// Where the ingestion of the Data Dragon versions stands: the one served, the count by
/// state, and the ones changed last.
/// </summary>
internal sealed class VersionOverview(LoDbDbContext db)
{
    private const int RecentCount = 6;

    public async Task<VersionState> ReadAsync(CancellationToken cancellationToken)
    {
        var versions = db.DdragonVersions.AsNoTracking();
        var counts = await versions
            .GroupBy(static version => version.Status)
            .Select(static state => new { state.Key, Count = state.Count() })
            .ToDictionaryAsync(static state => state.Key, static state => state.Count,
                cancellationToken);
        var current = await versions
            .Where(static version => version.PromotedAt != null)
            .OrderByDescending(static version => version.PromotedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var recent = await versions
            .OrderByDescending(static version => version.UpdatedAt)
            .Take(RecentCount)
            .ToListAsync(cancellationToken);
        return new VersionState
        {
            Current = current?.Version,
            PromotedAt = current?.PromotedAt,
            Discovered = counts.GetValueOrDefault(DdragonVersionStatus.Discovered),
            Ingesting = counts.GetValueOrDefault(DdragonVersionStatus.Ingesting),
            Ready = counts.GetValueOrDefault(DdragonVersionStatus.Ready),
            Failed = counts.GetValueOrDefault(DdragonVersionStatus.Failed),
            Recent = [.. recent.Select(Row)],
        };
    }

    private static VersionRow Row(DdragonVersion version) => new()
    {
        Version = version.Version,
        Status = NameOf(version.Status),
        Attempts = version.Attempts,
        UpdatedAt = version.UpdatedAt,
    };

    private static string NameOf(DdragonVersionStatus status) => status switch
    {
        DdragonVersionStatus.Discovered => "discovered",
        DdragonVersionStatus.Ingesting => "ingesting",
        DdragonVersionStatus.Ready => "ready",
        _ => "failed",
    };
}
