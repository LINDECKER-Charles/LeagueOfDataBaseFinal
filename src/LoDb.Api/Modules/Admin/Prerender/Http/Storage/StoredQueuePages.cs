namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// What the automatic queue holds of one epoch: its done pages no pass wrote again there, and
/// their bytes (none for a page written before the queue kept them).
/// </summary>
internal sealed record StoredQueuePages(int Epoch, long Pages, long Bytes);
