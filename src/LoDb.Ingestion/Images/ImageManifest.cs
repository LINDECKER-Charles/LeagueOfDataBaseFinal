using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Images;

/// <summary>
/// Reads the verdicts <c>ddragon_asset</c> holds for given images of a version.
/// </summary>
/// <remarks>
/// A key missing from the table was never tried, or failed transiently: only those are
/// fetched. One query per call, whatever the number of images.
/// </remarks>
internal sealed class ImageManifest(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The recorded rows among <paramref name="images"/>, by (type, key).</summary>
    public async Task<Dictionary<(string Type, string Key), DdragonAsset>> FindAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            return [];
        }

        var types = images.Select(static image => image.ManifestType).Distinct().ToArray();
        var keys = images.Select(static image => image.File).Distinct().ToArray();
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.DdragonAssets
                .AsNoTracking()
                .Where(asset => asset.Version == version.Value
                    && types.Contains(asset.Type)
                    && keys.Contains(asset.Key))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ToDictionary(static row => (row.Type, row.Key));
        }
    }

    /// <summary>The images the manifest does not settle yet.</summary>
    public async Task<List<DdragonImage>> UnrecordedAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        CancellationToken cancellationToken)
    {
        var recorded = await FindAsync(version, images, cancellationToken).ConfigureAwait(false);
        return
        [
            .. images.Where(image => !recorded.ContainsKey((image.ManifestType, image.File))),
        ];
    }
}
