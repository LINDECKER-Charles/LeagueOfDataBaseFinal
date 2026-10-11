using LoDb.Api.Modules.Accounts.Verification;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Accounts;

/// <summary>
/// The hourly erasure of the accounts that never verified their e-mail within a week
/// (ADR 0003): on one instance at a time, a batch per run, one summary line.
/// </summary>
/// <remarks>
/// Each erasure is journaled (<c>account.delete</c>, <c>reason</c>
/// <see cref="UnverifiedAccountPurge.Reason"/>), as the owner's own would be.
/// </remarks>
internal sealed class UnverifiedAccountPurgeJob(
    PeriodicJobServices services,
    IServiceScopeFactory scopes) : PeriodicJob(services)
{
    public override string Name => "accounts.unverified_purge";

    public override TimeSpan Period => TimeSpan.FromHours(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var purge = scope.ServiceProvider.GetRequiredService<UnverifiedAccountPurge>();
        return new(await purge.PurgeAsync(cancellationToken), "accounts");
    }
}
