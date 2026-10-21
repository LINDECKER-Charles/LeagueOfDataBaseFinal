namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// A refused page the catalogue lists, with the SSR's cumulative count of it and the pages of
/// its version.
/// </summary>
internal sealed record CountedPage(DemandPath Page, long Count, VersionPageIndex Index);
