using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Accounts;

/// <summary>
/// The daily deletion of the refresh token families whose every token ran out (ADR 0003): on
/// one instance at a time, one summary line.
/// </summary>
internal sealed class RefreshFamilyPurgeJob(
    PeriodicJobServices services,
    IServiceScopeFactory scopes) : PeriodicJob(services)
{
    public override string Name => "accounts.refresh_purge";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var families = scope.ServiceProvider.GetRequiredService<RefreshFamilies>();
        return new(await families.PurgeAsync(cancellationToken), "families");
    }
}
