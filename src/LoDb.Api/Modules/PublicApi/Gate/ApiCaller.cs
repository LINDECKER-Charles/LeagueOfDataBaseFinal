using LoDb.Api.Modules.PublicApi.Access;

namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>The key a request of <c>/v1</c> was admitted with, for its handler.</summary>
internal sealed record ApiCaller(ApiKeySnapshot Key)
{
    /// <summary>The caller the gate admitted.</summary>
    /// <exception cref="InvalidOperationException">The request did not pass the gate.</exception>
    public static ApiCaller Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Features.Get<ApiCaller>()
            ?? throw new InvalidOperationException("The request did not pass the /v1 gate.");
    }
}
