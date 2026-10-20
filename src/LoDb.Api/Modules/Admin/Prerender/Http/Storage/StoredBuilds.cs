namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// What <c>build/</c> holds, as the copies of the builds counted it: each file once, with the
/// build that stored it first.
/// </summary>
internal sealed record StoredBuilds(int Count, long Files, long Bytes);
