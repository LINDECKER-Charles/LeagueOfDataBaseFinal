namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// Reads the API's own cgroup (<see cref="CgroupCpuReader"/>; a fake in tests).
/// </summary>
internal interface ICgroupCpuReader
{
    /// <summary>The container's CPU now; null when it cannot be read.</summary>
    Task<CgroupCpuSample?> ReadAsync(CancellationToken cancellationToken);
}
