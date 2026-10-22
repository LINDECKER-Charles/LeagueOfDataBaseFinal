using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Coverage.Index;
using LoDb.Api.Modules.Admin.Prerender.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes.Planning;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// The older patches and what one epoch holds of each, (version, locale) by (version, locale),
/// with what the admin estimates a pass with, and what the index of states expects of each:
/// read rarely, when the launch dialog opens or the coverage tab shows, so it folds the epoch's
/// units without a cache.
/// </summary>
internal sealed class PatchCoverageReader(
    IPrerenderCatalog catalog,
    IEntityStateIndex index,
    EpochCoverage coverage,
    ExpectedPages expected,
    EstimateBasisReader estimates,
    IOptions<PrerenderOptions> options,
    TimeProvider clock)
{
    // One hexadecimal character per mask of the types held, lowercase.
    private const string HexDigits = "0123456789abcdef";

    private static readonly IReadOnlyList<string> Locales =
        [.. UiLocales.All.Select(UiLocales.Code)];

    /// <summary>
    /// The coverage of <paramref name="epoch"/>, the served one when null; any epoch reads, a
    /// retired one too. Below 1, the problem naming the epoch.
    /// </summary>
    public async Task<(PrerenderPatchesReport? Report, AccountProblem? Problem)> ReadAsync(
        int? epoch,
        CancellationToken cancellationToken)
    {
        var read = epoch ?? options.Value.ServedEpoch;
        if (read < PrerenderOptions.FirstEpoch)
        {
            var errors = new FieldErrors();
            errors.Add(PassRequestReader.EpochField, PassRequestReader.OutOfRange);
            return (null, errors.ToProblem());
        }

        var pinned = await catalog.PinnedVersionsAsync(cancellationToken);
        var map = await coverage.ReadAsync(read, cancellationToken);
        var perLocale = await expected.ReadAsync(TypeMask.All, null, cancellationToken);
        var indexed = IndexedPages.Of(await index.GetAsync(cancellationToken));
        return (new PrerenderPatchesReport
        {
            GeneratedAt = clock.GetUtcNow(),
            Epoch = read,
            ServedEpoch = options.Value.ServedEpoch,
            LatestVersion = (await catalog.LatestVersionAsync(cancellationToken))?.Value,
            Locales = Locales,
            Patches =
            [
                .. pinned.Select(version =>
                    RowOf(version, map, perLocale) with { Indexed = ViewOf(indexed, version) }),
            ],
            Coverage = map.Summary(pinned),
            Estimates = await estimates.ReadAsync(pinned, cancellationToken),
        }, null);
    }

    private static PrerenderIndexedPages? ViewOf(
        IReadOnlyDictionary<PatchVersion, IndexedPageCounts> indexed,
        PatchVersion version) =>
        indexed.TryGetValue(version, out var counts)
            ? new PrerenderIndexedPages { Openers = counts.Openers }
            : null;

    private static PrerenderPatchRow RowOf(
        PatchVersion version,
        CoverageMap map,
        IReadOnlyDictionary<string, int> perLocale)
    {
        var cells = Locales.Select(locale => map.CellOf(version.Value, locale)).ToList();
        var present = cells.OfType<CoverageCell>().ToList();
        return new PrerenderPatchRow
        {
            Version = version.Value,
            Cells = string.Concat(cells.Select(static cell => cell?.Code ?? UnitCells.Absent)),
            Types = string.Concat(cells.Select(static cell => HexDigits[cell?.Held ?? 0])),
            CompleteLocales = present.Count(static cell =>
                cell.Code is UnitCells.Completed or UnitCells.CompletedWithFailures),
            Pages = present.Sum(static cell => (long)cell.Pages),
            Bytes = present.Sum(static cell => cell.Bytes),
            FailedPages = present.Sum(static cell => (long)cell.FailedPages),
            PagesPerLocale = perLocale.TryGetValue(version.Value, out var pages) ? pages : null,
            LastEndedAt = present.Max(static cell => cell.LastEndedAt),
        };
    }
}
