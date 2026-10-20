namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// What the store holds of one (epoch, version, locale), as its units counted it while they
/// wrote: the most any unit of that triple wrote, since a later pass over the same pages
/// replaces them rather than adds to them.
/// </summary>
internal sealed record StoredUnit(int Epoch, string Version, int Pages, long Bytes);
