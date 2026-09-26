namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>
/// How a page view was captured, stored as <c>page</c> or <c>navigation</c>; each view is
/// captured by one of them only.
/// </summary>
public enum AnalyticsCaptureOrigin
{
    /// <summary>A page served as HTML (arrival or reload), seen through the nginx mirror.</summary>
    ServedPage,

    /// <summary>A navigation inside the application, reported by its beacon.</summary>
    Navigation,
}
