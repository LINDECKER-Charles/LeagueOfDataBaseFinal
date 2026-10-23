using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// The report of the pre-rendering, read-only: the settings of this host, the passes, what the
/// store holds and how much of the older patches each epoch not retired covers, from the
/// counters the engine keeps as it writes.
/// </summary>
/// <remarks>
/// The epoch ready to serve is judged by its coverage (<see cref="EpochReadiness"/>), not by a
/// whole pass completed: several targeted passes fill an epoch as well.
/// </remarks>
internal sealed class PrerenderJournal(
    PassHistory history,
    StorageCensus census,
    CoverageCache coverages,
    IPrerenderCatalog catalog,
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    IOptions<PrerenderPriceOptions> prices,
    IOptions<PageStoreOptions> store,
    TimeProvider clock)
{
    public async Task<PrerenderReport> ReportAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var served = options.Value.ServedEpoch;
        var passes = await history.LatestAsync(cancellationToken);
        var active = passes.FirstOrDefault(static facts => facts.Pass.IsActive);
        var writing = active?.Pass.Epoch;
        var views = passes.Select(facts => PassViews.Of(facts, now)).ToList();
        var covered = await CoveragesAsync(served, writing, cancellationToken);
        return new PrerenderReport
        {
            GeneratedAt = now,
            IsConfigured = activation.IsActive,
            EnqueuesPromotions = activation.EnqueuesPromotions,
            ServedEpoch = served,
            ReadyEpoch = EpochReadiness.Of(covered, served, writing),
            Current = active is null ? null : views.First(view => view.Id == active.Pass.Id),
            Passes = views,
            Storage = StorageLedger.Of(
                new StorageFacts
                {
                    Units = await census.UnitsAsync(cancellationToken),
                    Queue = await census.QueueAsync(cancellationToken),
                    Builds = await census.BuildsAsync(cancellationToken),
                    ServedEpoch = served,
                    WritingEpoch = writing,
                    Coverages = covered,
                },
                prices.Value),
            StoreBucket = store.Value.IsConfigured ? store.Value.Bucket : null,
        };
    }

    // The served epoch, the one written and every one awaiting the switch: the leftovers of
    // cancelled passes may leave several. None while Data Dragon cannot say which versions
    // are older patches: the report still reads, the coverage and the ready epoch unknown.
    private async Task<Dictionary<int, PrerenderEpochCoverage>> CoveragesAsync(
        int served,
        int? writing,
        CancellationToken cancellationToken)
    {
        if (await PinnedAsync(cancellationToken) is not { } pinned)
        {
            return [];
        }

        var epochs = (await history.EpochsAboveAsync(served, cancellationToken))
            .Append(served)
            .Concat(writing is { } written && written >= served ? [written] : [])
            .Distinct();
        var covered = new Dictionary<int, PrerenderEpochCoverage>();
        foreach (var epoch in epochs)
        {
            covered[epoch] = (await coverages.ReadAsync(epoch, cancellationToken)).Summary(pinned);
        }

        return covered;
    }

    private async Task<IReadOnlyList<PatchVersion>?> PinnedAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.PinnedVersionsAsync(cancellationToken);
        }
        catch (EgressException)
        {
            return null;
        }
    }
}
