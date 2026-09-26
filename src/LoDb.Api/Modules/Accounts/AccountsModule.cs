using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Google;
using LoDb.Api.Modules.Accounts.Google.Apps;
using LoDb.Api.Modules.Accounts.Google.Web;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Api.Modules.Accounts.Protection;
using LoDb.Api.Modules.Accounts.Recovery;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Session;
using LoDb.Api.Modules.Accounts.SignIn;
using LoDb.Api.Modules.Accounts.Verification;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// Accounts module: registration, sign-in, tokens and Google sign-in.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class AccountsModule
{
    public static IServiceCollection AddAccounts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AccountsOptions>()
            .Bind(configuration.GetSection(AccountsOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<AccountsOptions>, AccountsOptionsValidator>());
        services.AddAccountsSecurity()
            .AddSignInManager<LoDbSignInManager>()
            .AddDefaultTokenProviders();
        services.AddAccountsAuthentication().AddAccountsGoogle(configuration);
        services.TryAddSingleton<TrustedOrigins>();
        AddAccountServices(services);
        AddEndpoints(services);
        return services;
    }

    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup(AccountRoutes.Prefix).WithTags(AccountRoutes.Tag);
        MeEndpoint.Map(account);
        LoginEndpoint.Map(account);
        LogoutEndpoint.Map(account);
        TokenEndpoint.Map(account);
        RefreshEndpoint.Map(account);
        RegisterEndpoint.Map(account);
        VerifyEmailEndpoint.Map(account);
        ResendVerificationEndpoint.Map(account);
        ForgotPasswordEndpoint.Map(account);
        ResetPasswordEndpoint.Map(account);
        GoogleStartEndpoint.Map(account);
        GoogleExchangeEndpoint.Map(account);
        return endpoints;
    }

    private static void AddAccountServices(IServiceCollection services)
    {
        services.TryAddScoped<AccountAudit>();
        services.TryAddScoped<SessionReader>();
        services.TryAddScoped<AccountLookup>();
        services.TryAddScoped<PasswordSignIn>();
        services.TryAddScoped<LinkOrigin>();
        services.TryAddScoped<AccountMail>();
        services.TryAddScoped<MailThrottle>();
        services.TryAddScoped<GoogleProvisioner>();
        services.TryAddScoped<GoogleSignIn>();
        services.TryAddScoped<GoogleCallback>();
        services.TryAddScoped<GoogleCodeExchange>();
    }

    private static void AddEndpoints(IServiceCollection services)
    {
        services.TryAddScoped<MeEndpoint>();
        services.TryAddScoped<LoginEndpoint>();
        services.TryAddScoped<LogoutEndpoint>();
        services.TryAddScoped<TokenEndpoint>();
        services.TryAddScoped<RefreshEndpoint>();
        services.TryAddScoped<RegisterEndpoint>();
        services.TryAddScoped<VerifyEmailEndpoint>();
        services.TryAddScoped<ResendVerificationEndpoint>();
        services.TryAddScoped<ForgotPasswordEndpoint>();
        services.TryAddScoped<ResetPasswordEndpoint>();
        services.TryAddScoped<GoogleStartEndpoint>();
        services.TryAddScoped<GoogleExchangeEndpoint>();
    }
}
