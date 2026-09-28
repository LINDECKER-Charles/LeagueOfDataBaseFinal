using System.Diagnostics;
using LoDb.Api.Hosting.Telemetry;
using LoDb.Api.Modules.Admin.Monitoring.Views;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// Reads the figures of the API process in the process itself: no scrape of the metrics
/// endpoint, which the admin could not reach anyway.
/// </summary>
internal sealed class ProcessSampler(IConfiguration configuration, TimeProvider clock)
{
    private const int Generations = 3;

    public ProcessFigures Sample()
    {
        using var process = Process.GetCurrentProcess();
        var started = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        var revision = configuration[BuildInfoMetrics.RevisionKey];
        return new ProcessFigures
        {
            Version = BuildVersion.Current,
            Revision = string.IsNullOrWhiteSpace(revision) ? null : revision,
            UptimeSeconds = Math.Max(0, (long)(clock.GetUtcNow() - started).TotalSeconds),
            WorkingSetBytes = process.WorkingSet64,
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            CpuSeconds = process.TotalProcessorTime.TotalSeconds,
            Collections = [.. Enumerable.Range(0, Generations).Select(GC.CollectionCount)],
            ThreadPoolThreads = ThreadPool.ThreadCount,
            PendingWorkItems = ThreadPool.PendingWorkItemCount,
        };
    }
}
