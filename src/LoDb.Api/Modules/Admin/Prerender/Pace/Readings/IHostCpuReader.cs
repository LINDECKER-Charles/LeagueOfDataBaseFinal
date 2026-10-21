namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>Reads the host's CPU time (<see cref="ProcStatReader"/>; a fake in tests).</summary>
internal interface IHostCpuReader
{
    /// <summary>The host's CPU time now; null when it cannot be read.</summary>
    Task<HostCpuSample?> ReadAsync(CancellationToken cancellationToken);
}
