using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// The older patches and what one epoch holds of each, (version, locale) by (version, locale),
/// with what the admin estimates a pass with: read rarely, when the launch dialog opens or the
/// coverage tab shows, so it folds the epoch's units without a cache.
/// </summary>
/// <remarks>
/// TODO(B1): fold the units of the epoch's passes (EpochCoverage, CoverageFold) into each
/// patch's cells, types, pages and summary; the latest version (LatestVersionAsync); the bases
/// of the estimates (EstimateBasisReader). Until then every older patch reads absent.
/// </remarks>
internal sealed class PatchCoverageReader(
    IPrerenderCatalog catalog,
    IOptions<PrerenderOptions> options,
    IOptions<PrerenderPriceOptions> prices,
    TimeProvider clock)
{
    // The mask of the types held with its four bits clear, in hexadecimal.
    private const char NoTypeHeld = '0';

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
        return (ReportOf(read, pinned), null);
    }

    private PrerenderPatchesReport ReportOf(int epoch, IReadOnlyList<PatchVersion> pinned)
    {
        var locales = UiLocales.All.Select(UiLocales.Code).ToList();
        return new PrerenderPatchesReport
        {
            GeneratedAt = clock.GetUtcNow(),
            Epoch = epoch,
            ServedEpoch = options.Value.ServedEpoch,
            Locales = locales,
            Patches = [.. pinned.Select(version => AbsentRow(version, locales.Count))],
            Coverage = Absent(pinned.Count, locales.Count),
            Estimates = new PrerenderEstimateBasis
            {
                ConfiguredRendersPerMinute = options.Value.RendersPerMinute,
                PricePerGbMonthOneZone = prices.Value.PricePerGbMonthOneZone,
                Currency = PrerenderPriceOptions.Euro,
            },
        };
    }

    private static PrerenderPatchRow AbsentRow(PatchVersion version, int locales) => new()
    {
        Version = version.Value,
        Cells = new string(UnitCells.Absent, locales),
        Types = new string(NoTypeHeld, locales),
        CompleteLocales = 0,
        Pages = 0,
        Bytes = 0,
        FailedPages = 0,
    };

    private static PrerenderEpochCoverage Absent(int versions, int locales) => new()
    {
        PinnedVersions = versions,
        ExpectedUnits = versions * locales,
        CompleteUnits = 0,
        CleanUnits = 0,
        FailedUnits = 0,
        GapUnits = versions * locales,
        FailedPages = 0,
        CompleteVersions = 0,
        GapVersions = versions,
    };
}
