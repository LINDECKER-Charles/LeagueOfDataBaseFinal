using LoDb.Api.Modules.Accounts.Http;

namespace LoDb.Api.Modules.Accounts.Security.Admission;

/// <summary>
/// Answers a <see cref="HashingBusyException"/> of an endpoint that hashes with a 503 and its
/// <c>Retry-After</c>: a capacity refusal, distinct from the 429 of a rate limit.
/// </summary>
/// <remarks>
/// Caught here rather than by the host's exception handler, which would log every refusal:
/// the refusals are counted by the <see cref="SecurityMetrics"/>, never logged one by one.
/// </remarks>
internal sealed class HashingBusyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (HashingBusyException busy)
        {
            return AccountProblem.ServerBusy(busy.RetryAfter);
        }
    }
}
