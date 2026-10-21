namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// What the queue makes of one read of the SSR: the pages it keeps, those that are none of
/// the catalogue's (a junk URL, a page of the latest patch, a slug that is not the canonical
/// one), and those whose version's catalogue is not stored yet, judged at their next refusal.
/// </summary>
internal sealed record JudgedPages(IReadOnlyList<CountedPage> Accepted, int Rejected, int Deferred);
