using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Api.Modules.Accounts.Security.Limits;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// What protects the accounts' expensive and sensitive calls: the admission to password
/// hashing, the throttle of failed sign-ins and the rate limits of the accounts, with their
/// bounds read from the configuration.
/// </summary>
/// <remarks>The accounts module calls it once. It does no I/O and throws nothing.</remarks>
internal static class AccountsProtectionRegistration
{
    public static IServiceCollection AddAccountsProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HashingAdmissionOptions>()
            .Bind(configuration.GetSection(HashingAdmissionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static options => options.HasPositiveDelays(),
                $"{HashingAdmissionOptions.SectionName} needs a positive MaxWait and RetryAfter.")
            .ValidateOnStart();
        services.TryAddSingleton<HashingAdmission>();
        services.AddOptions<SignInLimitOptions>()
            .Bind(configuration.GetSection(SignInLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static options => options.HasUsableWindow(),
                $"{SignInLimitOptions.SectionName}:AccountWindow is too short.")
            .ValidateOnStart();
        services.TryAddSingleton<LoginThrottle>();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<SignInLimitOptions>>(AccountRateLimits.AddTo);
        return services;
    }
}
