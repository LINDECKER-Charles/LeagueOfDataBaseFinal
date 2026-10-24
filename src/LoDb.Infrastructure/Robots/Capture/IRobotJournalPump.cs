namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>Turns the queued records into hits and writes them, batch by batch.</summary>
public interface IRobotJournalPump
{
    /// <summary>Waits until a record is queued; false once the queue is closed.</summary>
    ValueTask<bool> WaitAsync(CancellationToken cancellationToken);

    /// <summary>Drains the queue now.</summary>
    /// <returns>The hits written.</returns>
    Task<int> FlushAsync(CancellationToken cancellationToken);
}
