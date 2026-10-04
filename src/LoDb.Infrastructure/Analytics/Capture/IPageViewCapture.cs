namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Where the capture endpoints hand their views: an in-memory queue in front of the writer,
/// so that taking a view in never waits for the database.
/// </summary>
public interface IPageViewCapture
{
    /// <returns>False when the queue is full: the view is dropped, and counted as such.</returns>
    bool TryCapture(CapturedView view);
}
