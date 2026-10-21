namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// The CPU seconds and the renders of a group of servers, summed from what each reports since
/// it started: a counter that goes back (a server restarted) restarts its own count, and the
/// sum never goes back.
/// </summary>
internal sealed class CpuTally
{
    private readonly Dictionary<string, (double Cpu, long Renders)> _last =
        new(StringComparer.Ordinal);

    /// <summary>CPU seconds counted since the tally began.</summary>
    public double CpuSeconds { get; private set; }

    /// <summary>Renders counted since the tally began.</summary>
    public long Renders { get; private set; }

    /// <summary>Counts what <paramref name="source"/> did since its last report.</summary>
    public void Add(string source, double cpuSeconds, long renders)
    {
        if (_last.TryGetValue(source, out var before)
            && cpuSeconds >= before.Cpu
            && renders >= before.Renders)
        {
            CpuSeconds += cpuSeconds - before.Cpu;
            Renders += renders - before.Renders;
        }

        _last[source] = (cpuSeconds, renders);
    }
}
