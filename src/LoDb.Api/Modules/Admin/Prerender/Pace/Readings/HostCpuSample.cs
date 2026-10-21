namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// The host's CPU time since it booted, in ticks of <c>/proc/stat</c>: idle, stolen by the
/// hypervisor, and in all.
/// </summary>
internal sealed record HostCpuSample(long Idle, long Steal, long Total);
