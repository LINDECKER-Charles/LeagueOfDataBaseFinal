using LoDb.Infrastructure.Locks;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// What every <see cref="PeriodicJob"/> needs, in one registration: a concrete job takes
/// this and its own services, and passes this to the base constructor.
/// </summary>
public sealed class PeriodicJobServices(
    IDistributedLock distributedLock,
    IJobSchedule schedule,
    JobMetrics metrics,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    public IDistributedLock Lock { get; } = distributedLock;

    public IJobSchedule Schedule { get; } = schedule;

    public JobMetrics Metrics { get; } = metrics;

    public TimeProvider TimeProvider { get; } = timeProvider;

    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
