using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.Limits;
using LoDb.Api.Modules.PublicApi.Metering;

namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>
/// Admits a request of <c>/v1</c> in go-api's order: its key, then its rate limit, then,
/// unless the route is free, its quota, the handler last.
/// </summary>
/// <remarks>
/// The <c>X-RateLimit-*</c> headers are set as soon as the key is known, so the refusals
/// of the rate limit and of the quota carry them, and so does whatever the handler answers.
/// A billed request is counted once admitted, before its handler runs, as go-api counts it.
/// </remarks>
internal sealed class ApiAccess(
    ApiKeyDirectory directory,
    ApiRateLimiter limiter,
    ApiQuota quota,
    ApiKeyStore store,
    UsageMeter meter,
    PublicApiMetrics metrics)
{
    /// <summary>The refusal of the request, or null once it is admitted.</summary>
    /// <param name="context">The request, which gets its <see cref="ApiCaller"/>.</param>
    /// <param name="billed">Whether the route is paid for by the quota or a credit.</param>
    public async ValueTask<V1Error?> AdmitAsync(HttpContext context, bool billed)
    {
        ArgumentNullException.ThrowIfNull(context);
        var aborted = context.RequestAborted;
        var credential = ApiKeyCredential.Read(context.Request);
        if (credential.Length == 0)
        {
            return Refuse(V1Errors.MissingKey);
        }

        if (ApiKeyCredential.HashOf(credential) is not { } hash)
        {
            return Refuse(V1Errors.MalformedKey);
        }

        var key = await directory.FindAsync(hash, aborted);
        if (key is null)
        {
            return Refuse(V1Errors.UnknownKey);
        }

        if (!key.IsUsable)
        {
            return Refuse(V1Errors.RevokedKey);
        }

        context.Features.Set(new ApiCaller(key));
        var verdict = limiter.Acquire(key);
        verdict.WriteHeaders(context.Response.Headers);
        if (!verdict.Allowed)
        {
            return Refuse(V1Errors.RateLimitExceeded);
        }

        if (billed && !await ChargeAsync(key, aborted))
        {
            return Refuse(V1Errors.QuotaExhausted);
        }

        return null;
    }

    private async ValueTask<bool> ChargeAsync(ApiKeySnapshot key, CancellationToken aborted)
    {
        switch (quota.TryCharge(key))
        {
            case QuotaDecision.Plan:
                meter.Record(key.Id);
                return true;
            case QuotaDecision.NeedsCredit:
                var balance = await store.SpendCreditAsync(key.Id, aborted);
                quota.RecordCredit(key, balance);
                if (balance is null)
                {
                    return false;
                }

                meter.Record(key.Id);
                return true;
            default:
                return false;
        }
    }

    private V1Error Refuse(V1Error refusal)
    {
        metrics.RecordRefusal(refusal.Code);
        return refusal;
    }
}
