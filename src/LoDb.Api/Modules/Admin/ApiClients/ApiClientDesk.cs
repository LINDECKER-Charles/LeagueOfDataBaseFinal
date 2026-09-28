using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.PublicApi;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>
/// Revokes keys of the public API and credits them with prepaid requests, each action
/// recorded in the audit journal and reported to the key cache of <c>/v1</c>.
/// </summary>
/// <remarks>
/// Reported once committed, the change applies to the next request of the key, where the
/// legacy admin waited up to a minute for go-api's cache to expire.
/// </remarks>
internal sealed class ApiClientDesk(
    LoDbDbContext db,
    IApiKeyCache cache,
    IAuditLog audit,
    TimeProvider clock)
{
    public const string RequestsField = "requests";
    public const string OutOfRange = "out-of-range";

    /// <summary>The bounds of a manual credit, as in the legacy admin.</summary>
    public const long MinCredit = 1;

    public const long MaxCredit = 1_000_000;

    private const string RequestsKey = "requests";

    public async Task<AccountProblem?> RevokeAsync(int id, CancellationToken cancellationToken)
    {
        var (key, problem) = await ActiveKeyAsync(id, cancellationToken);
        if (key is null)
        {
            return problem;
        }

        key.IsActive = false;
        key.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate(key);
        await audit.RecordAsync(Line(AuditAction.AdminApiClientRevoke, key), cancellationToken);
        return null;
    }

    public async Task<(CreditReceipt? Receipt, AccountProblem? Problem)> CreditAsync(
        int id,
        long? requests,
        CancellationToken cancellationToken)
    {
        if (requests is not { } amount || amount is < MinCredit or > MaxCredit)
        {
            return (null, AdminProblems.Invalid(RequestsField, OutOfRange));
        }

        var (key, problem) = await ActiveKeyAsync(id, cancellationToken);
        if (key is null)
        {
            return (null, problem);
        }

        await GrantAsync(key, amount, cancellationToken);
        cache.Invalidate(key);
        var line = Line(AuditAction.AdminApiClientCredit, key) with
        {
            Meta = new Dictionary<string, object?> { [RequestsKey] = amount },
        };
        await audit.RecordAsync(line, cancellationToken);
        await db.Entry(key).ReloadAsync(cancellationToken);
        return (new CreditReceipt
        {
            CreditsBalance = key.CreditsBalance,
            RateLimitPerMin = key.RateLimitPerMin,
        }, null);
    }

    private async Task<(ApiKey? Key, AccountProblem? Problem)> ActiveKeyAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.FindAsync([id], cancellationToken);
        return key switch
        {
            null => (null, AdminProblems.NotFound(AdminProblems.ApiClientNotFound)),
            { IsActive: false } or { RevokedAt: not null } =>
                (null, AdminProblems.Conflict(AdminProblems.ApiClientRevoked)),
            _ => (key, null),
        };
    }

    // The path of a paid pack: the balance grows in the database itself, so a request of
    // /v1 spending a credit meanwhile is not lost, and a grant dates the requests.
    private async Task GrantAsync(ApiKey key, long requests, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        await db.ApiKeys
            .Where(row => row.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.CreditsBalance, row => row.CreditsBalance + requests)
                    .SetProperty(
                        row => row.RateLimitPerMin,
                        row => Math.Max(row.RateLimitPerMin, ApiPlans.CreditsRate)),
                cancellation);
        db.ApiCreditGrants.Add(new ApiCreditGrant
        {
            ApiKeyId = key.Id,
            Source = ApiCreditGrantSource.Admin,
            Requests = requests,
            PurchasedAt = now,
            ExpiresAt = now.AddMonths(ApiCreditGrant.ValidityMonths),
        });
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }

    private static AuditEvent Line(AuditAction action, ApiKey key) =>
        new() { Action = action, Target = AdminTargets.Of(key) };
}
