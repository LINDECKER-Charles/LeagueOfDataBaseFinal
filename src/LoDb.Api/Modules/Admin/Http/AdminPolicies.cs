using LoDb.Api.Modules.Accounts.Protection;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>
/// The policy of the second-factor enrollment, the one admin door the <c>Admin</c> policy
/// leaves open to a session opened with a password only.
/// </summary>
internal static class AdminPolicies
{
    /// <summary>
    /// An administrator signed in by session cookie, second factor or not: enough to enrol an
    /// authenticator, nothing else.
    /// </summary>
    public const string Enrollment = "AdminEnrollment";

    public static IServiceCollection AddAdminPolicies(this IServiceCollection services)
    {
        // The forgery guard first, as in every policy of the host. The enrollment signs the
        // session in again, so only the cookie of the web may open it.
        var enrollment = new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
            .AddRequirements(ForgeryRequirement.Instance)
            .RequireAuthenticatedUser()
            .RequireRole(Role.Admin)
            .Build();
        // Options only: the authorization services come with the web host, and the command
        // host, which has no endpoints, could not build them.
        services.Configure<AuthorizationOptions>(
            options => options.AddPolicy(Enrollment, enrollment));
        return services;
    }
}
