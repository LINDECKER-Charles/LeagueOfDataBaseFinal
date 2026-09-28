using LoDb.Api.Modules.Billing.Expiry;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Billing;

/// <summary>
/// The daily expiry of the API credits: a pack's requests last twelve months from its
/// purchase, which the legacy stack announced without ever applying.
/// </summary>
/// <remarks>
/// Not audited: the run's summary line and the log of each key settled are its trace.
/// </remarks>
internal sealed class CreditExpiryJob(PeriodicJobServices services, IServiceScopeFactory scopes)
    : PeriodicJob(services)
{
    public override string Name => "billing.credit_expiry";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var expiry = scope.ServiceProvider.GetRequiredService<CreditExpiry>();
        return new(await expiry.RunAsync(cancellationToken), "keys");
    }
}
