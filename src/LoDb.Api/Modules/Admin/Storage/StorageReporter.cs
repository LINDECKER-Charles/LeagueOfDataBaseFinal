using LoDb.Api.Hosting.Health;
using LoDb.Api.Modules.Admin.Storage.Views;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace LoDb.Api.Modules.Admin.Storage;

/// <summary>
/// Assembles the storage report, kept ten minutes: a walk of the whole root is too heavy to
/// run at every visit of the page; a refresh asks for a new one.
/// </summary>
/// <remarks>
/// The requests that overlap share one walk, which reads the database through a scope of
/// its own: the request that started it may end or be aborted first.
/// </remarks>
internal sealed partial class StorageReporter(
    HybridCache cache,
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<StorageReporter> logger)
{
    public const string MissingRoot = "The storage root is not set.";
    public const string UnreadableRoot = "The storage root could not be read.";

    private const string CacheKey = "lodb:admin:storage";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // The volume of this instance: another instance may mount another one.
    private static readonly HybridCacheEntryOptions CacheEntry = new()
    {
        Expiration = Lifetime,
        LocalCacheExpiration = Lifetime,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<StorageReport> ReportAsync(bool refresh, CancellationToken cancellation)
    {
        if (refresh)
        {
            await cache.RemoveAsync(CacheKey, cancellation);
        }

        return await cache.GetOrCreateAsync(
            CacheKey,
            this,
            static (reporter, cancel) => reporter.AssembleAsync(cancel),
            CacheEntry,
            cancellationToken: cancellation);
    }

    private async ValueTask<StorageReport> AssembleAsync(CancellationToken cancellation)
    {
        var root = configuration[StorageReadinessCheck.RootKey];
        if (string.IsNullOrWhiteSpace(root))
        {
            return Failed(MissingRoot);
        }

        StorageScan scan;
        try
        {
            // The walk is blocking file system work: off the request thread.
            var directory = new DirectoryInfo(root);
            scan = await Task.Run(() => StorageScan.Of(directory, cancellation), cancellation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogUnreadable(logger, exception);
            return Failed(UnreadableRoot);
        }

        return Report(scan, await LogicalRefsAsync(cancellation));
    }

    private async Task<long> LogicalRefsAsync(CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        return await db.DdragonAssets.AsNoTracking()
            .LongCountAsync(static asset => asset.Sha256 != null, cancellation);
    }

    private StorageReport Report(StorageScan scan, long logicalRefs)
    {
        var blobs = Blobs(scan);
        return new StorageReport
        {
            GeneratedAt = clock.GetUtcNow(),
            Ok = true,
            Objects = scan.Families.Objects,
            Bytes = scan.Families.Bytes,
            Families = scan.Families.Rows(),
            Blobs = blobs,
            Data = new DatasetFigures
            {
                ByVersion = scan.Versions.Rows(),
                ByLang = scan.Langs.Rows(),
                ByType = scan.Types.Rows(),
            },
            Dedup = Dedup(scan, blobs, logicalRefs),
            Largest = scan.Largest(),
            Timeline = scan.Timeline(),
            Coverage = scan.Coverage.Rows(),
        };
    }

    private StorageReport Failed(string error) =>
        Report(new StorageScan(), 0) with { Ok = false, Error = error };

    private static BlobFigures Blobs(StorageScan scan)
    {
        var siblings = scan.Webps.Where(webp => scan.Pngs.ContainsKey(webp.Key)).ToList();
        return new BlobFigures
        {
            ByExt = scan.Extensions.Rows(),
            Sources = scan.Pngs.Count,
            WebpSiblings = siblings.Count,
            WebpCoverage = scan.Pngs.Count > 0 ? (double)siblings.Count / scan.Pngs.Count : 0,
            SourceBytes = scan.Pngs.Values.Sum(),
            WebpBytes = siblings.Sum(static webp => webp.Value),
        };
    }

    // A reference beyond the first to a blob is a file the content addressing did not write.
    private static DedupFigures Dedup(StorageScan scan, BlobFigures blobs, long logicalRefs)
    {
        var physical = scan.Extensions.Objects - blobs.WebpSiblings;
        var mean = physical > 0 ? (scan.Extensions.Bytes - blobs.WebpBytes) / physical : 0;
        return new DedupFigures
        {
            LogicalRefs = logicalRefs,
            PhysicalBlobs = physical,
            Ratio = physical > 0 ? (double)logicalRefs / physical : 0,
            SavedBytesApprox = Math.Max(0, logicalRefs - physical) * mean,
        };
    }

    [LoggerMessage(
        EventName = "admin.storage.root_unreadable",
        Level = LogLevel.Warning,
        Message = "The storage root could not be walked for the storage report.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception);
}
