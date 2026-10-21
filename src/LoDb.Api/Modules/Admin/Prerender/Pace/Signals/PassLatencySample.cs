namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// The pass's renders over the last minute: how many, their median, and the lowest median of
/// the last 30 minutes (null before the first).
/// </summary>
internal sealed record PassLatencySample(
    int Renders,
    double? MedianSeconds,
    double? BaselineSeconds);
