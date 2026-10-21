using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Catalog.Reading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// What the launch dialog estimates a pass with, all measured here: the share of each type of
/// page, the bytes of a page, the pace of a former pass, the pass's ceiling and the price.
/// </summary>
/// <remarks>
/// The shares come from the catalog of the newest older patch a unit completed: its catalog is
/// stored, so reading it queues nothing. Read on a patch not stored, it would queue an
/// ingestion, a side effect a read must not have.
/// </remarks>
internal sealed class EstimateBasisReader(
    IDbContextFactory<LoDbDbContext> contexts,
    IPrerenderCatalog catalog,
    IOptions<PrerenderOptions> options,
    IOptions<PrerenderPriceOptions> prices,
    TimeProvider clock)
{
    /// <summary>The latest passes looked at for a pace.</summary>
    public const int PassesMeasured = 5;

    /// <summary>The running time a pass needs for its pace to stand for the next one.</summary>
    public const long MeasuredSeconds = 600;

    private const int ShareDecimals = 4;

    private static readonly ResourceType[] Types = Enum.GetValues<ResourceType>();

    /// <summary>
    /// The bases of the estimates, the older patches being <paramref name="pinned"/>.
    /// </summary>
    public async Task<PrerenderEstimateBasis> ReadAsync(
        IReadOnlyList<PatchVersion> pinned,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return new PrerenderEstimateBasis
        {
            TypeShares = await TypeSharesAsync(db, pinned, cancellationToken),
            BytesPerPage = await BytesPerPageAsync(db, cancellationToken),
            MeasuredPagesPerMinute = await MeasuredPaceAsync(db, cancellationToken),
            ConfiguredRendersPerMinute = options.Value.Pace.IsAdaptive
                ? options.Value.Pace.MaxRendersPerMinute
                : options.Value.RendersPerMinute,
            PricePerGbMonthOneZone = prices.Value.PricePerGbMonthOneZone,
            Currency = PrerenderPriceOptions.Euro,
        };
    }

    private async Task<PrerenderTypeShares?> TypeSharesAsync(
        LoDbDbContext db,
        IReadOnlyList<PatchVersion> pinned,
        CancellationToken cancellationToken)
    {
        var completed = await db.PrerenderUnits.AsNoTracking()
            .Where(static unit => unit.State == PrerenderUnitState.Completed)
            .Select(static unit => unit.Version)
            .Distinct()
            .ToListAsync(cancellationToken);
        var stored = completed.ToHashSet(StringComparer.Ordinal);
        if (pinned.FirstOrDefault(version => stored.Contains(version.Value)) is not { } newest)
        {
            return null;
        }

        var list = await catalog.PagesAsync(newest, cancellationToken);
        return list.Status == CatalogLoadStatus.Ready ? SharesOf(list.Paths) : null;
    }

    // The last share is what the others leave, so that the four sum to 1 once rounded.
    private static PrerenderTypeShares? SharesOf(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var shares = Types
            .Select(type => Math.Round(
                (decimal)PageTypes.Keep(paths, [type]).Count / paths.Count,
                ShareDecimals))
            .ToList();
        return new PrerenderTypeShares
        {
            Champions = shares[0],
            Items = shares[1],
            Runes = shares[2],
            Summoners = 1m - shares[0] - shares[1] - shares[2],
        };
    }

    // The mean over the completed units of the served epoch: the front of the served epoch is
    // the one the next pass renders.
    private async Task<long?> BytesPerPageAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var served = options.Value.ServedEpoch;
        var units = db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.State == PrerenderUnitState.Completed
                && unit.PagesWritten > 0
                && unit.Pass!.Epoch == served);
        var pages = await units.SumAsync(static unit => (long)unit.PagesWritten, cancellationToken);
        return pages == 0
            ? null
            : await units.SumAsync(static unit => unit.BytesWritten, cancellationToken) / pages;
    }

    private async Task<decimal?> MeasuredPaceAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var passes = await db.PrerenderPasses.AsNoTracking()
            .OrderByDescending(static pass => pass.CreatedAt)
            .ThenByDescending(static pass => pass.Id)
            .Take(PassesMeasured)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var measured = passes
            .Select(pass => (Pass: pass, Seconds: PassProgress.ActiveSeconds(pass, now)))
            .FirstOrDefault(static pass => pass.Seconds >= MeasuredSeconds);
        return measured.Pass is null
            ? null
            : PassProgress.Shown(PassProgress.PagesPerMinute(measured.Pass, measured.Seconds));
    }
}
