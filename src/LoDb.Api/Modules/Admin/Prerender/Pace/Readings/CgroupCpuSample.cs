namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// The CPU this container used and the time its quota throttled it, since it started, in
/// microseconds (<c>cpu.stat</c>).
/// </summary>
internal sealed record CgroupCpuSample(long UsageMicroseconds, long ThrottledMicroseconds);
