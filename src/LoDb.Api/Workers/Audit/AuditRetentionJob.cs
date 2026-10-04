using LoDb.Api.Modules.Audit.Retention;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Audit;

/// <summary>
/// The daily retention of the audit journal (ADR 0003): the entries older than six months are
/// deleted, on one instance at a time, without an operator running anything.
/// </summary>
/// <remarks>
/// Not audited, as in the legacy stack: the run's summary line and metrics are its trace. An
/// operator's purge, by contrast, is (<c>admin.logs_purge</c>).
/// </remarks>
internal sealed class AuditRetentionJob(PeriodicJobServices services, IServiceScopeFactory scopes)
    : PeriodicJob(services)
{
    public override string Name => "audit.retention";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var retention = scope.ServiceProvider.GetRequiredService<AuditRetention>();
        return new(await retention.DeleteExpiredAsync(cancellationToken), "entries");
    }
}
