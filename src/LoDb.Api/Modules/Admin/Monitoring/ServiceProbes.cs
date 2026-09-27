using System.Data.Common;
using LoDb.Api.Modules.Admin.Monitoring.Views;
using LoDb.Api.Modules.Admin.Storage;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// The dependencies of the API, checked by the readiness probes of the host, with the
/// version and size of the database when it answers, and the volume of the storage when its
/// report is kept.
/// </summary>
/// <remarks>
/// The legacy admin also probed go-fetcher and go-api, which the API replaces: they are
/// this process now, described by <see cref="ProcessSampler"/>.
/// </remarks>
internal sealed partial class ServiceProbes(
    HealthCheckService health,
    LoDbDbContext db,
    StorageReporter storage,
    ILogger<ServiceProbes> logger)
{
    private const string ReadyTag = "ready";
    private const string Postgres = "postgres";
    private const string Storage = "storage";
    private const int DetailLength = 140;

    public async Task<IReadOnlyList<ServiceProbe>> ProbeAsync(CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(
            static check => check.Tags.Contains(ReadyTag),
            cancellationToken);
        var probes = new List<ServiceProbe>();
        foreach (var (name, entry) in report.Entries.OrderBy(static entry => entry.Key))
        {
            var probe = new ServiceProbe
            {
                Name = name,
                Status = ProbeStatuses.Of(entry.Status),
                LatencyMs = (long)entry.Duration.TotalMilliseconds,
                Detail = Detail(entry),
            };
            probes.Add(entry.Status == HealthStatus.Healthy
                ? await DescribeAsync(probe, cancellationToken)
                : probe);
        }

        return probes;
    }

    private async Task<ServiceProbe> DescribeAsync(
        ServiceProbe probe,
        CancellationToken cancellationToken) => probe.Name switch
        {
            Postgres => await DescribeDatabaseAsync(probe, cancellationToken),
            Storage => await DescribeStorageAsync(probe, cancellationToken),
            _ => probe,
        };

    // A bonus the legacy admin showed when the storage report happened to be warm: walking
    // the root at every monitoring load would cost a full inventory.
    private async Task<ServiceProbe> DescribeStorageAsync(
        ServiceProbe probe,
        CancellationToken cancellationToken) =>
        await storage.KeptAsync(cancellationToken) is { Ok: true } report
            ? probe with { Objects = report.Objects, Bytes = report.Bytes }
            : probe;

    private async Task<ServiceProbe> DescribeDatabaseAsync(
        ServiceProbe probe,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await db.Database
                .SqlQuery<string>($"""SELECT current_setting('server_version') AS "Value" """)
                .SingleAsync(cancellationToken);
            var bytes = await db.Database
                .SqlQuery<long>($"""SELECT pg_database_size(current_database()) AS "Value" """)
                .SingleAsync(cancellationToken);
            return probe with { Version = version, DatabaseBytes = bytes };
        }
        catch (DbException exception)
        {
            LogUndescribed(logger, exception);
            return probe;
        }
    }

    private static string? Detail(HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Healthy)
        {
            return null;
        }

        var reason = entry.Description ?? entry.Exception?.Message ?? entry.Status.ToString();
        return reason.Length <= DetailLength ? reason : reason[..DetailLength];
    }

    [LoggerMessage(
        EventName = "admin.monitoring.database_undescribed",
        Level = LogLevel.Warning,
        Message = "The version and size of the database could not be read.")]
    private static partial void LogUndescribed(ILogger logger, Exception exception);
}
