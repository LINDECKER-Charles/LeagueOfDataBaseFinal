using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Jobs;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// Pulls from Umami, every hour and on one instance at a time, the views of the detail pages
/// per entity and day, for <c>/v1/trends</c> (<see cref="IAudiencePull"/>); one summary line
/// per run, as every periodic job.
/// </summary>
/// <remarks>
/// Dormant until the pull is configured (<see cref="AudienceOptions.IsConfigured"/>): it says
/// so once at start and never runs, so a host without Umami, the staging or a local stack,
/// neither fails nor reaches out. Once the read-only account exists, setting its secrets and
/// restarting the API wakes it.
/// </remarks>
internal sealed partial class AudienceTrendsWorker(
    PeriodicJobServices services,
    IAudiencePull pull,
    IOptions<AudienceOptions> options,
    ILogger<AudienceTrendsWorker> logger) : PeriodicJob(services)
{
    private const string Unit = "entity days";

    public override string Name => "audience.trends";

    public override TimeSpan Period => options.Value.PullPeriod;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            LogDormant(logger);
            return Task.CompletedTask;
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task<JobRunSummary> RunOnceAsync(
        CancellationToken cancellationToken) =>
        new(await pull.PullAsync(cancellationToken), Unit);

    [LoggerMessage(
        EventName = "audience.trends.dormant",
        Level = LogLevel.Information,
        Message = "The audience pull is not configured (LoDb:Audience): it stays idle.")]
    private static partial void LogDormant(ILogger logger);
}
