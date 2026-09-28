using LoDb.Api.Hosting;
using LoDb.Api.Modules.ClientPolicy.Policy;
using LoDb.Api.Modules.ClientPolicy.Versions;

namespace LoDb.Api.Modules.ClientPolicy.Gate;

/// <summary>
/// Answers <c>426 Upgrade Required</c> to an app whose <c>X-LoDb-Client</c> version is
/// below the minimum its policy publishes (ADR 0008). A request without the header, the
/// web's, is never concerned and costs no read of the policy.
/// </summary>
/// <remarks>
/// <para>
/// Only <c>/api</c> is gated, and not the policy itself, which an outdated app must read
/// to know what to update to.
/// </para>
/// <para>
/// A policy that cannot be read lets the request through: an outage of the database must
/// not lock every app out on top of it.
/// </para>
/// </remarks>
internal sealed partial class ClientVersionGate(
    RequestDelegate next,
    ClientPolicyMetrics metrics,
    ILogger<ClientVersionGate> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var client = IsGated(context.Request) ? ClientHeader.Read(context.Request) : null;
        var policy = client is null ? null : await PolicyOfAsync(context, client);
        if (client is null || policy is null || !IsBelowMinimum(client, policy))
        {
            await next(context);
            return;
        }

        metrics.RecordUpgradeRequired(client.Platform);
        await UpgradeRequiredProblem.WriteAsync(context, client, policy);
    }

    private static bool IsGated(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiPaths.App)
        && !request.Path.StartsWithSegments(ClientPolicyRoutes.Policy);

    private static bool IsBelowMinimum(AppClient client, PlatformPolicy policy) =>
        AppVersion.TryParse(policy.MinimumVersion, out var minimum)
        && client.Version.IsBelow(minimum);

    private async Task<PlatformPolicy?> PolicyOfAsync(HttpContext context, AppClient client)
    {
        var store = context.RequestServices.GetRequiredService<ClientPolicyStore>();
        try
        {
            var policy = await store.ReadAsync(context.RequestAborted);
            return policy.For(client.Platform);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPolicyUnavailable(logger, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "client_policy.read.failed",
        Level = LogLevel.Warning,
        Message = "The client policy could not be read; the request goes through unchecked.")]
    private static partial void LogPolicyUnavailable(ILogger logger, Exception exception);
}
