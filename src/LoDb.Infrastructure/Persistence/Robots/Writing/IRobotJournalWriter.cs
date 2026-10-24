namespace LoDb.Infrastructure.Persistence.Robots.Writing;

/// <summary>Writes a batch of the robots journal: its agents, its hits and its days.</summary>
public interface IRobotJournalWriter
{
    /// <summary>
    /// In one transaction: the agents upserted, the hits copied, their counts added to the
    /// days they fall on. Nothing is written when any of it fails.
    /// </summary>
    Task WriteAsync(IReadOnlyCollection<RobotHitRecord> hits, CancellationToken cancellationToken);
}
