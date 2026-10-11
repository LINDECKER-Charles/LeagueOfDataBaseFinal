using LoDb.Api.Modules.Accounts.Security.Authenticator;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Accounts;

/// <summary>
/// The daily protection of the second-factor secrets an earlier version stored in clear
/// (ADR 0003): on one instance at a time, one summary line. Its first run, at start, protects
/// whatever the deployment found; the next ones find nothing.
/// </summary>
internal sealed class AuthenticatorSecretsJob(
    PeriodicJobServices services,
    IServiceScopeFactory scopes) : PeriodicJob(services)
{
    public override string Name => "accounts.authenticator_secrets";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var upkeep = scope.ServiceProvider.GetRequiredService<AuthenticatorSecretsUpkeep>();
        return new(await upkeep.ProtectAsync(cancellationToken), "accounts");
    }
}
