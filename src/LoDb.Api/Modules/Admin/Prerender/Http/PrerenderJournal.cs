using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Infrastructure.PageStore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// The report of the pre-rendering, read-only: the settings of this host, the passes and what
/// the store holds, from the counters the engine keeps as it writes.
/// </summary>
internal sealed class PrerenderJournal(
    PassHistory history,
    StorageCensus census,
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
        return new PrerenderReport
        {
            GeneratedAt = now,
            IsConfigured = activation.IsActive,
            EnqueuesPromotions = activation.EnqueuesPromotions,
            ServedEpoch = served,
            // TODO(B1): the epoch ready by its coverage (EpochReadiness), and the coverage of
            // each epoch not retired in Storage (StorageFacts.Coverages).
            ReadyEpoch = await history.ReadyEpochAsync(served, writing, cancellationToken),
            Current = active is null ? null : views.First(view => view.Id == active.Pass.Id),
            Passes = views,
            Storage = StorageLedger.Of(
                new StorageFacts
                {
                    Units = await census.UnitsAsync(cancellationToken),
                    Builds = await census.BuildsAsync(cancellationToken),
                    ServedEpoch = served,
                    WritingEpoch = writing,
                },
                prices.Value),
            StoreBucket = store.Value.IsConfigured ? store.Value.Bucket : null,
        };
    }
}
