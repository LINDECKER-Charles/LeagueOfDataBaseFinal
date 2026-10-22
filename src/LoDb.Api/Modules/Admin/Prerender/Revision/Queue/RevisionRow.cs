namespace LoDb.Api.Modules.Admin.Prerender.Revision.Queue;

/// <summary>A pending page of the queue, the epoch it is written into, its failed tries.</summary>
internal sealed record RevisionRow(string Path, int Epoch, int Attempts);
