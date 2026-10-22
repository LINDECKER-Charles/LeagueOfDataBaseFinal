using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The visitors' SSR's <c>GET /refused</c> as src/server/demand/refused-pages.ts answers it:
/// cumulative counts, a change counter for the cursor, an instance drawn at each start.
/// </summary>
internal sealed class FakeRefusedPages : IRefusedPagesReader
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (long Count, long Seq)> _pages =
        new(StringComparer.Ordinal);
    private long _seq;

    public string Instance { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>Changes one answer carries at most, as the SSR's own bound.</summary>
    public int Limit { get; set; } = 10_000;

    /// <summary>Whether the SSR is out of reach: every read fails.</summary>
    public bool IsDown { get; set; }

    /// <summary>The reads made, with their cursor.</summary>
    public List<long> Reads { get; } = [];

    public void Refuse(string path, int times = 1)
    {
        lock (_gate)
        {
            _seq++;
            var count = _pages.GetValueOrDefault(path).Count + times;
            _pages[path] = (count, _seq);
        }
    }

    /// <summary>A restart: a new instance, every count from zero.</summary>
    public void Restart()
    {
        lock (_gate)
        {
            (_seq, Instance) = (0, Guid.NewGuid().ToString());
            _pages.Clear();
        }
    }

    public Task<RefusedPagesReport?> ReadAsync(
        Uri url,
        long after,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Reads.Add(after);
            return Task.FromResult(IsDown ? null : Report(after));
        }
    }

    private RefusedPagesReport Report(long after)
    {
        var changed = _pages.Where(page => page.Value.Seq > after)
            .OrderBy(static page => page.Value.Seq)
            .ToList();
        var kept = changed.Take(Limit).ToList();
        var isComplete = kept.Count == changed.Count;
        return new RefusedPagesReport
        {
            Instance = Instance,
            Cursor = isComplete ? Math.Max(_seq, after) : kept[^1].Value.Seq,
            IsComplete = isComplete,
            Pages =
            [
                .. kept.Select(static page =>
                    new RefusedPageCount(page.Key, page.Value.Count)),
            ],
        };
    }
}
