using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Api.Modules.Accounts.Security.Invitations;
using LoDb.Api.Modules.Accounts.Security.Limits;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// What protects the accounts' expensive and sensitive calls: the admission to password
/// hashing, the throttle of failed sign-ins, the rate limits of the accounts and the
/// invitations that open the enrolment of an administrator's authenticator, with their
/// bounds read from the configuration.
/// </summary>
/// <remarks>
/// The accounts module calls it once, so the command host has it too. It does no I/O and
/// throws nothing.
/// </remarks>
internal static class AccountsProtectionRegistration
{
    public static IServiceCollection AddAccountsProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddHashingAdmission(services, configuration);
        AddSignInLimits(services, configuration);
        AddMfaInvitations(services, configuration);
        return services;
    }

    private static void AddHashingAdmission(
        IServiceCollection services,
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
    }

    private static void AddSignInLimits(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SignInLimitOptions>()
            .Bind(configuration.GetSection(SignInLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static options => options.HasUsableWindow(),
                $"{SignInLimitOptions.SectionName}:AccountWindow is too short.")
            .Validate<IOptions<HashingAdmissionOptions>>(
                StaysUnderLockout,
                $"{SignInLimitOptions.SectionName}:SignInFailuresPerIdentifier, plus the "
                    + "hashing permits beyond the first, must stay under the lockout.")
            .ValidateOnStart();
        services.TryAddSingleton<LoginThrottle>();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<SignInLimitOptions>>(AccountRateLimits.AddTo);
    }

    private static bool StaysUnderLockout(
        SignInLimitOptions limits,
        IOptions<HashingAdmissionOptions> hashing)
    {
        try
        {
            return limits.StaysUnderLockout(hashing.Value.Permits);
        }
        catch (OptionsValidationException)
        {
            // Bounds of the hashing that are invalid stop the host through their own check.
            return true;
        }
    }

    private static void AddMfaInvitations(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MfaInvitationOptions>()
            .Bind(configuration.GetSection(MfaInvitationOptions.SectionName))
            .Validate(
                static options => options.HasUsableLifetime(),
                $"{MfaInvitationOptions.SectionName}:Lifetime must be positive and short.")
            .ValidateOnStart();
        services.TryAddSingleton<MfaInvitations>();
    }
}
