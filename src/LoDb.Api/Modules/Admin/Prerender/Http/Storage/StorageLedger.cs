using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// What the store holds, epoch by epoch and version by version, and what it is estimated to
/// cost each month: a pure function of the counters and the prices.
/// </summary>
/// <remarks>
/// <para>
/// Pages are written in One Zone and bundles in Multi-AZ (ADR 0011), each at its price. The
/// totals add the exact costs before rounding, so they do not drift from the parts' sum by
/// more than the rounding of each.
/// </para>
/// <para>
/// The totals leave the retired epochs out: each costs until the lifecycle has deleted it,
/// nine days or more for a whole epoch, but the API cannot see that end, and counting them for
/// good would overstate the bill for good. Their rows keep their own cost meanwhile.
/// </para>
/// </remarks>
internal static class StorageLedger
{
    private const decimal BytesPerGb = 1_000_000_000m;
    private const int CostDecimals = 4;

    public static PrerenderStorageView Of(StorageFacts facts, PrerenderPriceOptions prices)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(prices);
        var epochs = EpochsOf(facts, prices.PricePerGbMonthOneZone);
        var kept = epochs.Where(static epoch => epoch.State != EpochStates.Retired).ToList();
        var builds = facts.Builds;
        var buildCost = ExactCost(builds.Bytes, prices.PricePerGbMonthMultiAz);
        var pagesCost = kept.Sum(epoch => ExactCost(epoch.Bytes, prices.PricePerGbMonthOneZone));
        return new PrerenderStorageView
        {
            Epochs = epochs,
            Builds = new PrerenderBuildVolume
            {
                Count = builds.Count,
                Files = builds.Files,
                Bytes = builds.Bytes,
                MonthlyCost = Rounded(buildCost),
            },
            ServedVersions = VersionsOf(facts),
            Pages = kept.Sum(static epoch => epoch.Pages),
            Bytes = kept.Sum(static epoch => epoch.Bytes) + builds.Bytes,
            MonthlyCost = Rounded(pagesCost + buildCost),
            PricePerGbMonthOneZone = prices.PricePerGbMonthOneZone,
            PricePerGbMonthMultiAz = prices.PricePerGbMonthMultiAz,
            Currency = PrerenderPriceOptions.Euro,
            ExcludesEgress = true,
        };
    }

    // Every epoch holding pages, and the served, written and covered ones even while empty: the
    // panel shows the epoch it fills from its first page on.
    private static List<PrerenderEpochVolume> EpochsOf(StorageFacts facts, decimal price)
    {
        var byEpoch = facts.Units.ToLookup(static unit => unit.Epoch);
        var epochs = byEpoch.Select(static units => units.Key)
            .Append(facts.ServedEpoch)
            .Concat(facts.WritingEpoch is { } writing ? [writing] : [])
            .Concat(facts.Coverages.Keys)
            .Distinct()
            .OrderDescending();
        return
        [
            .. epochs.Select(epoch =>
            {
                var bytes = byEpoch[epoch].Sum(static unit => unit.Bytes);
                return new PrerenderEpochVolume
                {
                    Epoch = epoch,
                    State = EpochStates.Of(epoch, facts),
                    IsWriting = epoch == facts.WritingEpoch,
                    Pages = byEpoch[epoch].Sum(static unit => (long)unit.Pages),
                    Bytes = bytes,
                    MonthlyCost = Rounded(ExactCost(bytes, price)),
                    Coverage = facts.Coverages.GetValueOrDefault(epoch),
                };
            }),
        ];
    }

    private static List<PrerenderVersionVolume> VersionsOf(StorageFacts facts) =>
    [
        .. facts.Units
            .Where(unit => unit.Epoch == facts.ServedEpoch)
            .GroupBy(static unit => unit.Version, StringComparer.Ordinal)
            .Select(static version => new PrerenderVersionVolume
            {
                Version = version.Key,
                Locales = version.Count(),
                Pages = version.Sum(static unit => (long)unit.Pages),
                Bytes = version.Sum(static unit => unit.Bytes),
            })
            .OrderByDescending(static version => SortKeyOf(version.Version)),
    ];

    // Versions in the units are the ones the engine wrote, all well formed; a row edited by
    // hand sorts last rather than failing the report.
    private static PatchVersion? SortKeyOf(string version) =>
        PatchVersion.TryParse(version, out var parsed) ? parsed : null;

    private static decimal ExactCost(long bytes, decimal pricePerGbMonth) =>
        bytes / BytesPerGb * pricePerGbMonth;

    private static decimal Rounded(decimal cost) => Math.Round(cost, CostDecimals);
}
