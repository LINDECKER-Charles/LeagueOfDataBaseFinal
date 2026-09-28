namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>Figures of the API process, read in the process itself.</summary>
internal sealed record ProcessFigures
{
    /// <summary>Version of the API assembly.</summary>
    public required string Version { get; init; }

    /// <summary>Source revision of the image; null outside of one.</summary>
    public string? Revision { get; init; }

    public required long UptimeSeconds { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long ManagedHeapBytes { get; init; }

    public required double CpuSeconds { get; init; }

    /// <summary>Garbage collections so far, generation 0, 1 then 2.</summary>
    public required IReadOnlyList<int> Collections { get; init; }

    public required int ThreadPoolThreads { get; init; }

    public required long PendingWorkItems { get; init; }
}
