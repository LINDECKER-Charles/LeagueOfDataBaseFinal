using LoDb.Api.Hosting;
using LoDb.Api.Modules.PublicApi.Keys.Contracts;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// <c>/api/account/api-key</c>: reads, creates, regenerates and revokes the key of the
/// signed-in account.
/// </summary>
/// <remarks>
/// Creating a key needs a verified e-mail, as in the legacy portal: a key opens the paid
/// API. Regenerating does not, since it only replaces the secret of a key already issued,
/// such as one a payment issued.
/// </remarks>
internal sealed class ApiKeyEndpoints(
    UserManager<User> users,
    KeyOverviews overviews,
    OwnedKeys keys)
{
    private const string Regenerate = "/regenerate";

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet(
                string.Empty,
                static ([FromServices] ApiKeyEndpoints endpoints, HttpContext context) =>
                    endpoints.ReadAsync(context))
            .WithName("getApiKey")
            .WithSummary("The active API key of the account, with its usage; never its secret.");
        group.MapPost(
                string.Empty,
                static (
                    [FromBody] CreateApiKeyRequest request,
                    [FromServices] ApiKeyEndpoints endpoints,
                    HttpContext context) => endpoints.CreateAsync(request, context))
            .RequireAuthorization(AuthorizationPolicies.VerifiedEmail)
            .WithName("createApiKey")
            .WithSummary("Issues the account's API key, whose secret this answer alone shows.")
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost(
                Regenerate,
                static ([FromServices] ApiKeyEndpoints endpoints, HttpContext context) =>
                    endpoints.RegenerateAsync(context))
            .WithName("regenerateApiKey")
            .WithSummary("Replaces the secret of the account's API key, keeping its rights.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete(
                string.Empty,
                static ([FromServices] ApiKeyEndpoints endpoints, HttpContext context) =>
                    endpoints.RevokeAsync(context))
            .WithName("revokeApiKey")
            .WithSummary("Revokes the account's API key, refused by /v1 from the next request.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public async Task<Results<Ok<ApiKeyState>, ApiKeyProblem>> ReadAsync(HttpContext context)
    {
        if (await AccountOfAsync(context) is not { } owner)
        {
            return ApiKeyProblem.SignedOut();
        }

        var key = await overviews.OfUserAsync(owner.Id, context.RequestAborted);
        return TypedResults.Ok(new ApiKeyState { Key = key });
    }

    public async Task<Results<Ok<IssuedApiKey>, ApiKeyProblem>> CreateAsync(
        CreateApiKeyRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await AccountOfAsync(context) is not { } owner)
        {
            return ApiKeyProblem.SignedOut();
        }

        var issue = await keys.CreateAsync(owner.Id, request.Name, context.RequestAborted);
        return issue is null
            ? ApiKeyProblem.KeyExists()
            : TypedResults.Ok(await IssuedAsync(issue, context.RequestAborted));
    }

    public async Task<Results<Ok<IssuedApiKey>, ApiKeyProblem>> RegenerateAsync(
        HttpContext context)
    {
        if (await AccountOfAsync(context) is not { } owner)
        {
            return ApiKeyProblem.SignedOut();
        }

        var issue = await keys.RegenerateAsync(owner.Id, context.RequestAborted);
        return issue is null
            ? ApiKeyProblem.NoKey()
            : TypedResults.Ok(await IssuedAsync(issue, context.RequestAborted));
    }

    public async Task<Results<NoContent, ApiKeyProblem>> RevokeAsync(HttpContext context)
    {
        if (await AccountOfAsync(context) is not { } owner)
        {
            return ApiKeyProblem.SignedOut();
        }

        return await keys.RevokeAsync(owner.Id, context.RequestAborted)
            ? TypedResults.NoContent()
            : ApiKeyProblem.NoKey();
    }

    // Gone or banned, a session not revalidated yet may still name the account.
    private async Task<User?> AccountOfAsync(HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        return user is { IsBanned: false } ? user : null;
    }

    private async Task<IssuedApiKey> IssuedAsync(
        KeyIssue issue,
        CancellationToken cancellationToken) => new()
    {
        Secret = issue.Secret,
        Key = await overviews.OfAsync(issue.Key, cancellationToken),
    };
}
