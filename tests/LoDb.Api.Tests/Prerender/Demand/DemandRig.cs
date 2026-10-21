using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;
using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The automatic queue on the pass's harness: the SSR's refused pages faked, every other part
/// the API's own; by default one older patch, 16.11.1, of six pages.
/// </summary>
internal sealed class DemandRig : IAsyncDisposable
{
    public const string Url = "http://lodb-web-ssr:9464/refused";
    public static readonly PatchVersion Version = PatchVersion.Parse("16.11.1");

    private DemandRig(PrerenderHarness harness, DemandCollector collector)
    {
        Harness = harness;
        Collector = collector;
    }

    public PrerenderHarness Harness { get; }

    public FakeRefusedPages Refused { get; } = new();

    public DemandCollector Collector { get; private set; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<DemandRig> StartAsync(
        PostgresContainerFixture postgres,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>
        {
            [$"{DemandOptions.SectionName}:RefusedPagesUrl"] = Url,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            all[key] = value;
        }

        var harness = await PrerenderHarness.StartAsync(postgres, settings: all);
        harness.Catalog.Pinned.Add(Version);
        var rig = new DemandRig(harness, null!);
        rig.Collector = rig.NewCollector();
        return rig;
    }

    /// <summary>A path of the default version: <c>/{locale}/16.11.1/{page}</c>.</summary>
    public static string PathOf(string page, string locale = "en") =>
        $"/{locale}/{Version.Value}/{page}";

    /// <summary>
    /// A collector of its own, as after a restart of the API: its cursor at zero.
    /// </summary>
    public DemandCollector NewCollector() => new(
        Harness.Get<DemandActivation>(),
        Refused,
        Harness.Get<DemandValidator>(),
        Harness.Get<DemandUpserts>(),
        Harness.Get<DemandQueue>(),
        Harness.Get<DemandMetrics>(),
        Harness.Get<IOptions<PrerenderOptions>>(),
        NullLogger<DemandCollector>.Instance);

    public Task CollectAsync() => Collector.CollectAsync(Token);

    public Task DrainAsync() => Harness.Get<DemandDrain>().RunAsync(Token);

    public async Task<PrerenderDemandPage?> RowAsync(string path)
    {
        await using var db = Harness.Database.CreateContext();
        return await db.PrerenderDemand.AsNoTracking()
            .SingleOrDefaultAsync(page => page.Path == path, Token);
    }

    public async Task<List<PrerenderDemandPage>> RowsAsync()
    {
        await using var db = Harness.Database.CreateContext();
        return await db.PrerenderDemand.AsNoTracking()
            .OrderBy(static page => page.Path)
            .ToListAsync(Token);
    }

    public ValueTask DisposeAsync() => Harness.DisposeAsync();
}
