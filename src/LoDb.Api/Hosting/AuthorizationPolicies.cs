namespace LoDb.Api.Hosting;

/// <summary>
/// Named authorization policies of the API.
/// </summary>
/// <remarks>
/// Authentication, authorization and antiforgery are wired into the host from the start,
/// so the security chantier only declares its policies here. A module adds its schemes
/// through <c>services.AddAuthentication()</c> and may add policies through
/// <c>services.AddAuthorizationBuilder()</c>.
/// </remarks>
internal static class AuthorizationPolicies
{
    public static IServiceCollection AddLoDbAuthorization(
        this IServiceCollection services,
        IConfiguration configuration) => services.AddAuthorization();
}
