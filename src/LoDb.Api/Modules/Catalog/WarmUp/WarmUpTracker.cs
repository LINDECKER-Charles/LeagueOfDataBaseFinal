using System.Threading.Channels;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// The state of a warm-up, from its first frame to its last: counts the images as the
/// ingestion reports them, and publishes each new state to the frames of the stream.
/// </summary>
/// <remarks>
/// Reports come from the fetching threads, hence the lock. Publishing never blocks: the
/// frames keep the latest state only. The last frame keeps the counts reached, so a run cut
/// short never shows a client its progress falling back to nothing.
/// </remarks>
internal sealed class WarmUpTracker : IProgress<DdragonImage>
{
    private readonly Lock gate = new();
    private readonly IReadOnlyList<ResourceType> resources;
    private readonly ChannelWriter<WarmUpProgress> frames;
    private readonly Dictionary<(string Type, string File), PlannedImage> unsettled = [];
    private readonly Dictionary<ResourceType, int> totals;
    private readonly Dictionary<ResourceType, int> settled;
    private readonly HashSet<ResourceType> ready = [];
    private WarmUpStage stage = WarmUpStage.Preparing;
    private WarmUpLanding? latest;
    private string? failure;

    /// <param name="resources">The lists of the run, each once, in the requested order.</param>
    /// <param name="frames">Where each new state goes.</param>
    public WarmUpTracker(
        IReadOnlyList<ResourceType> resources,
        ChannelWriter<WarmUpProgress> frames)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(frames);
        this.resources = resources;
        this.frames = frames;
        totals = resources.ToDictionary(static resource => resource, static _ => 0);
        settled = resources.ToDictionary(static resource => resource, static _ => 0);
    }

    public IReadOnlyList<ResourceType> Resources => resources;

    /// <summary>Publishes the first frame: the datasets are prepared, nothing is counted.</summary>
    public void Prepare()
    {
        lock (gate)
        {
            Publish();
        }
    }

    /// <summary>
    /// Takes the images left to fetch and publishes the first frame of the images stage; a
    /// resource with none is ready at once.
    /// </summary>
    public void Start(IReadOnlyList<PlannedImage> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        lock (gate)
        {
            foreach (var resource in resources)
            {
                totals[resource] = pending.Count(planned => planned.Resource == resource);
            }

            foreach (var planned in pending)
            {
                unsettled[KeyOf(planned.Image)] = planned;
            }

            ready.UnionWith(resources.Where(resource => totals[resource] == 0));
            stage = WarmUpStage.Images;
            Publish();
        }
    }

    /// <summary>The images of <paramref name="resource"/> not reported yet.</summary>
    public IReadOnlyList<DdragonImage> PendingOf(ResourceType resource)
    {
        lock (gate)
        {
            return
            [
                .. unsettled.Values
                    .Where(planned => planned.Resource == resource)
                    .Select(static planned => planned.Image),
            ];
        }
    }

    public void Report(DdragonImage value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            if (!unsettled.Remove(KeyOf(value), out var planned))
            {
                return;
            }

            settled[planned.Resource]++;
            latest = new WarmUpLanding { Name = planned.Name, Resource = planned.Resource };
            Publish();
        }
    }

    /// <summary>
    /// Marks a resource ready once its ingestion returned: the images another call fetched
    /// meanwhile, which were only waited for, count as settled.
    /// </summary>
    public void Complete(ResourceType resource)
    {
        lock (gate)
        {
            settled[resource] = totals[resource];
            ready.Add(resource);
            Publish();
        }
    }

    /// <summary>
    /// Ends the run, the catalog open. A resource left uncompleted keeps its count and stays
    /// unready: its images are pending, for the pages to fill.
    /// </summary>
    public void Finish()
    {
        lock (gate)
        {
            stage = WarmUpStage.Done;
            Publish();
        }
    }

    /// <summary>Ends the run with the problem code that cut it short.</summary>
    public void Fail(string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        lock (gate)
        {
            stage = WarmUpStage.Failed;
            failure = code;
            Publish();
        }
    }

    private static (string Type, string File) KeyOf(DdragonImage image) =>
        (image.ManifestType, image.File);

    private void Publish() => frames.TryWrite(new WarmUpProgress
    {
        Stage = stage,
        Total = totals.Values.Sum(),
        Settled = settled.Values.Sum(),
        Resources =
        [
            .. resources.Select(resource => new WarmUpResource
            {
                Resource = resource,
                Total = totals[resource],
                Settled = settled[resource],
                Ready = ready.Contains(resource),
            }),
        ],
        Latest = latest,
        Failure = failure,
    });
}
