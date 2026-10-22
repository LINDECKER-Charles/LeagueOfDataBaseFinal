using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// The versions the site promoted, as <c>ddragon_version</c> dates them: which one left the
/// latest rank when another took it.
/// </summary>
/// <remarks>
/// A handful of rows, one per patch the new stack promoted: read whole and ordered as versions,
/// which their text does not order.
/// </remarks>
internal sealed class PromotedVersions(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The version <paramref name="promoted"/> replaced as the latest one.</summary>
    public async Task<PatchVersion?> PreviousOfAsync(
        PatchVersion promoted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(promoted);
        var versions = await NewestFirstAsync(cancellationToken);
        return versions.FirstOrDefault(version => version < promoted);
    }

    /// <summary>The version the last promotion made the latest; null before any.</summary>
    public async Task<PatchVersion?> LatestAsync(CancellationToken cancellationToken)
    {
        var versions = await NewestFirstAsync(cancellationToken);
        return versions.FirstOrDefault();
    }

    /// <summary>The version the latest one replaced, at the last promotion.</summary>
    public async Task<PatchVersion?> PreviousOfLatestAsync(CancellationToken cancellationToken)
    {
        var versions = await NewestFirstAsync(cancellationToken);
        return versions.Skip(1).FirstOrDefault();
    }

    private async Task<List<PatchVersion>> NewestFirstAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var promoted = await db.DdragonVersions.AsNoTracking()
            .Where(static row => row.PromotedAt != null)
            .Select(static row => row.Version)
            .ToListAsync(cancellationToken);
        return
        [
            .. promoted
                .Select(static text =>
                    PatchVersion.TryParse(text, out var version) ? version : null)
                .OfType<PatchVersion>()
                .OrderDescending(),
        ];
    }
}
