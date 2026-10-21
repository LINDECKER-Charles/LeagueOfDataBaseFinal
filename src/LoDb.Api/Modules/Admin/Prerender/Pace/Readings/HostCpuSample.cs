namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// The host's CPU time since it booted, in ticks of <c>/proc/stat</c>: idle, stolen by the
/// hypervisor, and in all; and the CPUs counted, 0 when unknown.
/// </summary>
internal sealed record HostCpuSample(long Idle, long Steal, long Total, int Cpus = 0);
