namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>A pending page the queue's worker takes, and its failed tries so far.</summary>
internal sealed record DemandRow(string Path, long Demand, int Attempts);
