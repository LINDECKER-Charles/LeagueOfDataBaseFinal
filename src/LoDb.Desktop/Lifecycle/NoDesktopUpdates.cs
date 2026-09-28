namespace LoDb.Desktop.Lifecycle;

/// <summary>Updates of a build without an update feed: never any to apply.</summary>
internal sealed class NoDesktopUpdates : IDesktopUpdates
{
    public UpdateSnapshot Current => UpdateSnapshot.None;

    public Task<bool> ApplyAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public void PrepareExit()
    {
        // Nothing is ever pending.
    }
}
