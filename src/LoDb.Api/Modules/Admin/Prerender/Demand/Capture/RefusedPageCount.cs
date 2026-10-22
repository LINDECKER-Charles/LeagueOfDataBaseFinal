namespace LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

/// <summary>
/// A page the SSR refused, <c>/fr/15.13.1/champions/Aatrox</c>, and its refusals since the SSR
/// started (or since the path came back after an eviction): cumulative, as a counter's.
/// </summary>
internal sealed record RefusedPageCount(string Path, long Count);
