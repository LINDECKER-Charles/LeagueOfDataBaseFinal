namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>Where the journal's endpoint hands over what nginx reported.</summary>
public interface IRobotJournal
{
    /// <summary>
    /// Queues the records for the pump without waiting; those beyond the queue's capacity are
    /// dropped and counted, as are the lines that could not be read.
    /// </summary>
    void Take(IReadOnlyCollection<JournalRecord> records, int unreadable);
}
