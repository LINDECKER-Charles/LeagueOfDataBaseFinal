using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.ClientPolicy.Policy;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.ClientPolicy.Publishing;

/// <summary>
/// <c>PUT /api/admin/client-policy/{platform}</c>: an administrator publishes the policy of
/// one app, as <c>client-policy publish</c> does from a release workflow. The group asks for
/// the <c>Admin</c> policy.
/// </summary>
internal sealed class PublishPolicyEndpoint(ClientPolicyStore store)
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut(
                "/{platform}",
                static (
                    string platform,
                    [FromBody] PublishPolicyRequest request,
                    [FromServices] PublishPolicyEndpoint endpoint,
                    CancellationToken cancellationToken) =>
                    endpoint.PublishAsync(platform, request, cancellationToken))
            .WithName("publishClientPolicy")
            .WithSummary("Replaces the minimum, latest version and bundle of one app.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<PlatformPolicy>, AccountProblem>> PublishAsync(
        string platform,
        PublishPolicyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ClientPlatforms.TryParse(platform, out var app))
        {
            return new AccountProblem
            {
                Status = StatusCodes.Status404NotFound,
                Code = PolicyErrors.UnknownPlatform,
                Title = "No app answers to this name.",
            };
        }

        var errors = PublishPolicyRules.Check(app, request);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await store.PublishAsync(app, request, cancellationToken));
    }
}
