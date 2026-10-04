namespace LoDb.Infrastructure.Persistence.Analytics.Partitions;

/// <summary>
/// The daily partitions of <c>analytics_event</c>, one per UTC day, named
/// <c>analytics_event_yyyyMMdd</c> (see <see cref="AnalyticsPartitionNames"/>).
/// </summary>
/// <remarks>
/// A view is refused when its day has no partition, so the partitions are created ahead of
/// time; retention drops them whole. Creations and drops run in one transaction under an
/// advisory lock: two instances never race on the same partition.
/// </remarks>
public interface IAnalyticsPartitions
{
    /// <summary>The days that have a partition, in order.</summary>
    Task<IReadOnlyList<DateOnly>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the missing partitions from <paramref name="first"/> to
    /// <paramref name="last"/>, both included.
    /// </summary>
    /// <returns>The number of partitions created; zero when all existed.</returns>
    Task<int> CreateAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the partitions of the days before <paramref name="firstKept"/>, with their events.
    /// </summary>
    /// <returns>The number of partitions dropped.</returns>
    Task<int> DropBeforeAsync(DateOnly firstKept, CancellationToken cancellationToken);
}
