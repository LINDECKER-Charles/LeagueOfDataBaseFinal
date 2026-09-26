namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// When each periodic job last started and succeeded, on any instance and across restarts.
/// </summary>
public interface IJobSchedule
{
    /// <summary>
    /// Records a start at <paramref name="now"/> unless the job already started less than
    /// <paramref name="interval"/> before. Of two concurrent callers, one at most gets true.
    /// </summary>
    Task<bool> TryStartAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken);

    Task RecordSuccessAsync(string job, DateTimeOffset now, CancellationToken cancellationToken);
}
