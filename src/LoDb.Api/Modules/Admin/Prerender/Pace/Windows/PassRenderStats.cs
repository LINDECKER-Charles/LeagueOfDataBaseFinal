namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The pass's renders over the last minute: how many ended, their median wall time (null with
/// none), and how many renders it started.
/// </summary>
internal sealed record PassRenderStats(int Renders, double? MedianSeconds, int Starts);
