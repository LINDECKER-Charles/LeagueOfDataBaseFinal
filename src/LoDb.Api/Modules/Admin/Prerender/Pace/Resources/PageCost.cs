namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// What a pre-rendered page costs the host in CPU, measured in production on 2026-10-21 (a
/// unit of 901 pages, then a pass at 76 to 84 pages a minute): what the admin predicts a pace
/// from until the renderers' own figures are measured.
/// </summary>
/// <remarks>
/// The renderer spent 0.09 to 0.14 s of CPU per page, about 0.1 s in a render of 0.1 to 0.15 s
/// of wall time; the API and Postgres together some 0.03 s more (the page's data, its upload
/// to the store, its counters), within the noise of the visitors' load.
/// </remarks>
internal static class PageCost
{
    /// <summary>A renderer's CPU seconds per page, until measured here.</summary>
    public const double DefaultRendererCpuSecondsPerPage = 0.10;

    /// <summary>The API's and Postgres's CPU seconds per page, never measured apart.</summary>
    public const double StackCpuSecondsPerPage = 0.03;

    /// <summary>Renders over 10 minutes for the measured figure to stand.</summary>
    public const int RendersToMeasure = 60;
}
