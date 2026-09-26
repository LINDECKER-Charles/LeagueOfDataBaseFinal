using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Protection;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Hosting;

/// <summary>
/// Named authorization policies of the API.
/// </summary>
/// <remarks>
/// <para>
/// Authentication, authorization and antiforgery are wired into the host from the start,
/// so the security chantier only declares its policies here. A module adds its schemes
/// through <c>services.AddAuthentication()</c> and may add policies through
/// <c>services.AddAuthorizationBuilder()</c>.
/// </para>
/// <para>
/// Every policy, the fallback one of the endpoints that name none included, first checks
/// that the request is not forged. An endpoint must therefore never be marked
/// <c>AllowAnonymous</c>: that skips the whole of authorization, the forgery guard with it.
/// </para>
/// </remarks>
internal static class AuthorizationPolicies
{
    /// <summary>A signed-in account, by session cookie or bearer token: the default.</summary>
    public const string Authenticated = "Authenticated";

    /// <summary>A signed-in account whose e-mail is verified, as stored now.</summary>
    public const string VerifiedEmail = "VerifiedEmail";

    /// <summary>An administrator whose session was opened with a second factor.</summary>
    public const string Admin = "Admin";

    public static IServiceCollection AddLoDbAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, ForgeryHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, VerifiedEmailHandler>());
        services.Replace(ServiceDescriptor.Singleton<
            IAuthorizationMiddlewareResultHandler,
            ProblemAuthorizationResultHandler>());

        var signedIn = SignedIn().Build();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(Unforged().Build())
            .SetDefaultPolicy(signedIn)
            .AddPolicy(Authenticated, signedIn)
            .AddPolicy(
                VerifiedEmail,
                SignedIn().AddRequirements(VerifiedEmailRequirement.Instance).Build())
            .AddPolicy(
                Admin,
                SignedIn()
                    .RequireRole(Role.Admin)
                    .RequireClaim(
                        AuthenticationMethods.ClaimType,
                        AuthenticationMethods.MultiFactor)
                    .Build());
        return services;
    }

    private static AuthorizationPolicyBuilder Unforged() =>
        new AuthorizationPolicyBuilder().AddRequirements(ForgeryRequirement.Instance);

    private static AuthorizationPolicyBuilder SignedIn() =>
        Unforged().RequireAuthenticatedUser();
}
